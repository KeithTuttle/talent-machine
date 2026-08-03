using System.Net;
using System.Text;
using System.Text.Json;

namespace TalentMachine.Api.Services;

/// <summary>One conflict the model pulled out of a messy sheet (validated/repaired).</summary>
public record AiConflictRow(
    string PerformerName, string MatchedName, string Type,
    string StartDate, string EndDate, List<string> Weekdays, string Reason);

/// <summary>Result of an AI import cleanup — same Configured/Ok shape as the other AI services.</summary>
public record AiImportResult(bool Configured, bool Ok, List<AiConflictRow> Rows);

/// <summary>Roster entry handed to the model so it can match names.</summary>
public record RosterMember(string Name);

/// <summary>
/// Uses Gemini to extract performer scheduling conflicts from a messy, arbitrarily
/// shaped spreadsheet: matches names to the cast roster (nicknames/typos), infers
/// dates, splits multiple weekdays, and interprets freeform text. Same plumbing as
/// <see cref="RehearsalAiService"/> (server-side key, structured JSON, self-healing
/// model discovery, always validate/repair). Proposals are reviewed by the user
/// before anything is imported — nothing here writes to the database.
/// </summary>
public class ConflictImportAiService
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<ConflictImportAiService> _logger;
    private readonly string? _apiKey;
    private readonly string _configuredModel;

    private static string? _resolvedModel;
    private static readonly SemaphoreSlim _modelLock = new(1, 1);

    public ConflictImportAiService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<ConflictImportAiService> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
        _apiKey = config["Gemini:ApiKey"];
        _configuredModel = string.IsNullOrWhiteSpace(config["Gemini:Model"]) ? "gemini-flash-lite-latest" : config["Gemini:Model"]!;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<AiImportResult> ExtractAsync(
        List<List<string>> rows, List<RosterMember> roster, CancellationToken ct = default)
    {
        if (!IsConfigured) return new AiImportResult(false, false, new());
        try
        {
            var raw = await CallGeminiAsync(BuildPrompt(rows, roster), ct);
            return new AiImportResult(true, true, ParseAndRepair(raw, roster));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini conflict-import extraction failed");
            return new AiImportResult(true, false, new());
        }
    }

    private async Task<string> CallGeminiAsync(string prompt, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = new
                {
                    type = "ARRAY",
                    items = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            performerName = new { type = "STRING" },
                            matchedName = new { type = "STRING" },
                            type = new { type = "STRING" },
                            startDate = new { type = "STRING" },
                            endDate = new { type = "STRING" },
                            weekdays = new { type = "ARRAY", items = new { type = "STRING" } },
                            reason = new { type = "STRING" },
                        },
                        required = new[] { "performerName", "matchedName", "type", "startDate", "endDate", "weekdays", "reason" },
                    },
                },
            },
        });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        var client = _httpFactory.CreateClient();

        var model = _resolvedModel ?? _configuredModel;
        var resp = await GenerateAsync(client, model, payload, cts.Token);
        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            resp.Dispose();
            var discovered = await ResolveWorkingModelAsync(client, cts.Token)
                ?? throw new InvalidOperationException($"Gemini model '{model}' not found and no alternative could be discovered.");
            _logger.LogInformation("Gemini model '{Old}' not found; using '{New}'.", model, discovered);
            _resolvedModel = discovered;
            resp = await GenerateAsync(client, discovered, payload, cts.Token);
        }

        using (resp)
        {
            resp.EnsureSuccessStatusCode();
            using var stream = await resp.Content.ReadAsStreamAsync(cts.Token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);
            var parts = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts");
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("thought", out var th) && th.ValueKind == JsonValueKind.True) continue;
                if (part.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                {
                    var s = t.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) return s!;
                }
            }
            throw new InvalidOperationException("no text part in Gemini response");
        }
    }

    private async Task<HttpResponseMessage> GenerateAsync(
        HttpClient client, string model, string payload, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/models/{model}:generateContent")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("x-goog-api-key", _apiKey);
        return await client.SendAsync(request, ct);
    }

    private async Task<string?> ResolveWorkingModelAsync(HttpClient client, CancellationToken ct)
    {
        await _modelLock.WaitAsync(ct);
        try
        {
            if (_resolvedModel is not null) return _resolvedModel;

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/models");
            request.Headers.TryAddWithoutValidation("x-goog-api-key", _apiKey);
            using var resp = await client.SendAsync(request, ct);
            if (!resp.IsSuccessStatusCode) return null;

            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("models", out var models)) return null;

            string[] disallowed =
            {
                "tts", "image", "audio", "embed", "vision", "live", "aqa", "learnlm",
                "omni", "nano", "lyria", "veo", "imagen", "customtools", "gemma", "deep-research",
            };
            bool IsPlainText(string id) => !disallowed.Any(bad => id.Contains(bad, StringComparison.OrdinalIgnoreCase));

            var candidates = new List<string>();
            foreach (var m in models.EnumerateArray())
            {
                if (!m.TryGetProperty("supportedGenerationMethods", out var methods)) continue;
                if (!methods.EnumerateArray().Any(x => x.GetString() == "generateContent")) continue;
                var name = m.GetProperty("name").GetString() ?? "";
                var id = name.StartsWith("models/") ? name["models/".Length..] : name;
                if (IsPlainText(id)) candidates.Add(id);
            }

            bool Has(string c, string s) => c.Contains(s, StringComparison.OrdinalIgnoreCase);
            return candidates.FirstOrDefault(c => c == "gemini-flash-lite-latest")
                ?? candidates.FirstOrDefault(c => c == "gemini-flash-latest")
                ?? candidates.FirstOrDefault(c => c.EndsWith("-flash-lite-latest", StringComparison.OrdinalIgnoreCase))
                ?? candidates.FirstOrDefault(c => c.EndsWith("-flash-latest", StringComparison.OrdinalIgnoreCase))
                ?? candidates.FirstOrDefault(c => Has(c, "flash") && Has(c, "lite") && !Has(c, "preview"))
                ?? candidates.FirstOrDefault(c => Has(c, "flash") && !Has(c, "preview"))
                ?? candidates.FirstOrDefault(c => Has(c, "flash"))
                ?? candidates.FirstOrDefault();
        }
        catch
        {
            return null;
        }
        finally
        {
            _modelLock.Release();
        }
    }

    private static string BuildPrompt(List<List<string>> rows, List<RosterMember> roster)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You extract performer scheduling conflicts from a messy spreadsheet for a youth musical theater production.");
        sb.AppendLine("The sheet may have arbitrary column order, a header row, freeform notes, nicknames, typos, and various date formats.");
        sb.AppendLine();
        sb.AppendLine("CRITICAL: Each DATA row below describes ONE performer. Produce exactly one output object per data");
        sb.AppendLine("row that names a performer. NEVER merge two different performers into one object. Skip a header row");
        sb.AppendLine("(labels like \"name\", \"date\", \"notes\") and skip empty rows. Do NOT invent conflicts.");
        sb.AppendLine();
        sb.AppendLine("Cast roster — set matchedName to the EXACT roster name the row refers to (resolve nicknames/typos/initials,");
        sb.AppendLine("e.g. \"Liv\"→Olivia, \"E Johnson\"→Emma Johnson), or \"\" if you truly can't tell:");
        foreach (var r in roster) sb.AppendLine($"- {r.Name}");
        sb.AppendLine();
        sb.AppendLine("For each output object:");
        sb.AppendLine("- performerName: the name exactly as written in that row.");
        sb.AppendLine("- matchedName: the exact roster name, or \"\".");
        sb.AppendLine("- type: \"Weekly\" if it recurs on weekday(s), else \"OneOff\".");
        sb.AppendLine($"- startDate/endDate: yyyy-MM-dd. Parse informal dates too (\"Aug 10-14 2026\" → 2026-08-10 / 2026-08-14; \"6/3\" → {DateTime.Today.Year}-06-03). If the year is missing assume {DateTime.Today.Year}. A single day sets only startDate; a range sets both; \"\" if no date is stated.");
        sb.AppendLine("- weekdays: full weekday names when recurring (that row's days, e.g. [\"Monday\",\"Wednesday\"]); [] otherwise.");
        sb.AppendLine("- reason: the activity or reason from the row (e.g. \"family trip\", \"soccer\", \"guitar lessons\"); pull it from any notes/reason text. \"\" only if none.");
        sb.AppendLine();
        sb.AppendLine("Format example (illustrative names — use the roster above for real matches):");
        sb.AppendLine("  \"Row 5: Jamie R | out Aug 3-5 | cousin's wedding\"  →");
        sb.AppendLine("  {\"performerName\":\"Jamie R\",\"matchedName\":\"Jamie Rivera\",\"type\":\"OneOff\",\"startDate\":\"" + DateTime.Today.Year + "-08-03\",\"endDate\":\"" + DateTime.Today.Year + "-08-05\",\"weekdays\":[],\"reason\":\"cousin's wedding\"}");
        sb.AppendLine("  \"Row 6: Sam K | tues & thurs dance class\"  →");
        sb.AppendLine("  {\"performerName\":\"Sam K\",\"matchedName\":\"Samuel Kim\",\"type\":\"Weekly\",\"startDate\":\"\",\"endDate\":\"\",\"weekdays\":[\"Tuesday\",\"Thursday\"],\"reason\":\"dance class\"}");
        sb.AppendLine();
        sb.AppendLine("Data — process each row independently:");
        // Number rows and use a visible separator so the model keeps them distinct.
        for (var i = 0; i < rows.Count && i < 200; i++)
            sb.AppendLine($"Row {i + 1}: {string.Join(" | ", rows[i])}");
        sb.AppendLine();
        sb.AppendLine("Return one JSON object per performer row (not the header).");
        return sb.ToString();
    }

    private static List<AiConflictRow> ParseAndRepair(string raw, List<RosterMember> roster)
    {
        var json = raw.Trim();
        if (!json.StartsWith('['))
        {
            var start = json.IndexOf('[');
            var end = json.LastIndexOf(']');
            if (start >= 0 && end > start) json = json[start..(end + 1)];
        }

        // Map lowercased roster names → canonical, so matchedName is forced onto the roster.
        var canon = roster
            .GroupBy(r => r.Name.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First().Name);

        var result = new List<AiConflictRow>();
        using var doc = JsonDocument.Parse(json);
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var performerName = Str(el, "performerName");
            if (string.IsNullOrWhiteSpace(performerName)) continue;

            var matchedRaw = Str(el, "matchedName");
            var matched = !string.IsNullOrWhiteSpace(matchedRaw)
                && canon.TryGetValue(matchedRaw.Trim().ToLowerInvariant(), out var c) ? c : "";

            var weekdays = new List<string>();
            if (el.TryGetProperty("weekdays", out var wd) && wd.ValueKind == JsonValueKind.Array)
            {
                foreach (var w in wd.EnumerateArray())
                {
                    var s = w.GetString();
                    if (!string.IsNullOrWhiteSpace(s)
                        && Enum.TryParse<DayOfWeek>(s.Trim(), ignoreCase: true, out var day)
                        && !weekdays.Contains(day.ToString()))
                        weekdays.Add(day.ToString());
                }
            }

            var type = weekdays.Count > 0 ? "Weekly"
                : Str(el, "type").Equals("Weekly", StringComparison.OrdinalIgnoreCase) ? "Weekly" : "OneOff";

            var start = DateOnly.TryParse(Str(el, "startDate"), out var sd) ? sd.ToString("yyyy-MM-dd") : "";
            var end = DateOnly.TryParse(Str(el, "endDate"), out var ed) ? ed.ToString("yyyy-MM-dd") : "";

            result.Add(new AiConflictRow(
                performerName.Trim(), matched, type, start, end, weekdays, Str(el, "reason").Trim()));
        }
        return result;
    }

    private static string Str(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
