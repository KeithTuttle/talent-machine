using System.Net;
using System.Text;
using System.Text.Json;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Services;

/// <summary>One suggested rehearsal slot (validated/repaired before returning).</summary>
public record SuggestedSlot(
    string Date, string StartTime, string EndTime, string Type, int? MusicalNumberId, string? Notes);

/// <summary>
/// Result of an AI schedule suggestion. <c>Configured</c> is false when no Gemini
/// key is set (the client hides the feature only then); <c>Ok</c> is false when
/// the call failed (the client shows a retry note).
/// </summary>
public record RehearsalSuggestion(bool Configured, bool Ok, List<SuggestedSlot> Slots);

/// <summary>Context handed to the model, gathered by the controller.</summary>
public record SuggestContext(
    string ProductionTitle,
    DateOnly From,
    DateOnly To,
    string? UserPrompt,
    List<MusicalNumber> Numbers,
    ILookup<int, int> CastByNumber,
    List<Performer> Cast,
    List<Conflict> Conflicts);

/// <summary>
/// Suggests a rehearsal schedule via Google Gemini's free tier — same service
/// shape as dance-manager's FormationAiService: server-side key only, structured
/// JSON output, output always validated/repaired, and self-healing model
/// discovery if the configured model name is ever retired (404 → ListModels).
/// </summary>
public class RehearsalAiService
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<RehearsalAiService> _logger;
    private readonly string? _apiKey;
    private readonly string _configuredModel;

    // Process-wide cache of a model confirmed to work, once discovered.
    private static string? _resolvedModel;
    private static readonly SemaphoreSlim _modelLock = new(1, 1);

    public RehearsalAiService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<RehearsalAiService> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
        _apiKey = config["Gemini:ApiKey"];
        // "gemini-flash-lite-latest" is an alias that always tracks the current
        // lite flash model: fast, free-tier friendly, no pinned version to go
        // stale. Override via Gemini:Model.
        _configuredModel = string.IsNullOrWhiteSpace(config["Gemini:Model"]) ? "gemini-flash-lite-latest" : config["Gemini:Model"]!;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<RehearsalSuggestion> SuggestAsync(SuggestContext ctx, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return new RehearsalSuggestion(false, false, new());

        try
        {
            var raw = await CallGeminiAsync(ctx, ct);
            var slots = ParseAndRepair(raw, ctx);
            return new RehearsalSuggestion(true, true, slots);
        }
        catch (Exception ex)
        {
            // Timeout, network, bad key, safety block, unparseable output — all
            // degrade to "not ok"; the client offers a retry instead of hiding.
            _logger.LogWarning(ex, "Gemini rehearsal suggestion failed");
            return new RehearsalSuggestion(true, false, new());
        }
    }

    private async Task<string> CallGeminiAsync(SuggestContext ctx, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            contents = new[] { new { parts = new[] { new { text = BuildPrompt(ctx) } } } },
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
                            date = new { type = "STRING" },
                            startTime = new { type = "STRING" },
                            endTime = new { type = "STRING" },
                            type = new { type = "STRING" },
                            musicalNumberId = new { type = "INTEGER" },
                            notes = new { type = "STRING" },
                        },
                        required = new[] { "date", "startTime", "endTime", "type" },
                    },
                },
            },
        });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        // Generous: current flash models often "think" before answering (10–20s).
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        var client = _httpFactory.CreateClient();

        var model = _resolvedModel ?? _configuredModel;
        var resp = await GenerateAsync(client, model, payload, cts.Token);

        // Model not found → discover a current one, cache it, retry once.
        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            resp.Dispose();
            var discovered = await ResolveWorkingModelAsync(client, cts.Token);
            if (discovered is null)
                throw new InvalidOperationException($"Gemini model '{model}' not found and no alternative could be discovered.");
            _logger.LogInformation("Gemini model '{Old}' not found; using '{New}'.", model, discovered);
            _resolvedModel = discovered;
            resp = await GenerateAsync(client, discovered, payload, cts.Token);
        }

        using (resp)
        {
            resp.EnsureSuccessStatusCode();
            using var stream = await resp.Content.ReadAsStreamAsync(cts.Token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);
            // Thinking models may prepend a part flagged "thought": true — skip
            // those and take the first real text part.
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

    /// <summary>Ask Gemini which models exist and pick a current flash model that supports generateContent.</summary>
    private async Task<string?> ResolveWorkingModelAsync(HttpClient client, CancellationToken ct)
    {
        await _modelLock.WaitAsync(ct);
        try
        {
            if (_resolvedModel is not null) return _resolvedModel; // another request already resolved it

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/models");
            request.Headers.TryAddWithoutValidation("x-goog-api-key", _apiKey);
            using var resp = await client.SendAsync(request, ct);
            if (!resp.IsSuccessStatusCode) return null;

            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("models", out var models)) return null;

            // Specialized / non-text variants that support generateContent but reject
            // our text+JSON request (image, speech, embeddings, tool-only, etc.).
            string[] disallowed =
            {
                "tts", "image", "audio", "embed", "vision", "live", "aqa", "learnlm",
                "omni", "nano", "lyria", "veo", "imagen", "customtools", "gemma", "deep-research",
            };
            bool IsPlainText(string id) =>
                !disallowed.Any(bad => id.Contains(bad, StringComparison.OrdinalIgnoreCase));

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

    private static string BuildPrompt(SuggestContext ctx)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You plan rehearsal schedules for a youth musical theater production.");
        sb.AppendLine($"Production: {ctx.ProductionTitle}.");
        sb.AppendLine($"Plan rehearsals ONLY between {ctx.From:yyyy-MM-dd} and {ctx.To:yyyy-MM-dd} (inclusive).");
        sb.AppendLine("Rules:");
        sb.AppendLine("- date must be yyyy-MM-dd; startTime/endTime must be 24-hour HH:mm.");
        sb.AppendLine("- type must be one of: Music, Dance, Blocking, Runthrough, Other.");
        sb.AppendLine("- musicalNumberId must be one of the ids listed below, or omitted for a general session.");
        sb.AppendLine("- Slots on the same day must not overlap in time; order them sensibly.");
        sb.AppendLine("- Prefer days/times where the kids in a number have no conflicts; note it in `notes` when a slot unavoidably clashes with someone's conflict.");
        sb.AppendLine("- Give bigger/ensemble numbers more time than small ones.");
        sb.AppendLine();
        sb.AppendLine("Numbers (id: title — cast size):");
        foreach (var n in ctx.Numbers)
            sb.AppendLine($"- {n.Id}: {n.Title} — {ctx.CastByNumber[n.Id].Count()} kids");
        sb.AppendLine();
        sb.AppendLine("Performer conflicts in this range (performer: unavailable):");
        var name = ctx.Cast.ToDictionary(p => p.Id, p => $"{p.FirstName} {p.LastName}".Trim());
        var any = false;
        foreach (var c in ctx.Conflicts)
        {
            var who = name.TryGetValue(c.PerformerId, out var n) ? n : $"#{c.PerformerId}";
            var when = c.Type == ConflictType.Weekly
                ? $"every {c.Weekday}" + (c.EndDate is null ? "" : $" until {c.EndDate:yyyy-MM-dd}")
                : c.EndDate is null ? $"{c.StartDate:yyyy-MM-dd}" : $"{c.StartDate:yyyy-MM-dd} to {c.EndDate:yyyy-MM-dd}";
            sb.AppendLine($"- {who}: {when}{(string.IsNullOrWhiteSpace(c.Reason) ? "" : $" ({c.Reason})")}");
            any = true;
        }
        if (!any) sb.AppendLine("- none recorded");
        sb.AppendLine();
        sb.Append("Director's request: ");
        sb.AppendLine(string.IsNullOrWhiteSpace(ctx.UserPrompt)
            ? "a sensible schedule covering the numbers that most need work."
            : ctx.UserPrompt!.Trim());
        sb.AppendLine();
        sb.AppendLine("Return the slots as the JSON array.");
        return sb.ToString();
    }

    private static List<SuggestedSlot> ParseAndRepair(string raw, SuggestContext ctx)
    {
        // Be lenient: if the model wrapped the array in prose, grab the first [...] block.
        var json = raw.Trim();
        if (!json.StartsWith('['))
        {
            var start = json.IndexOf('[');
            var end = json.LastIndexOf(']');
            if (start >= 0 && end > start) json = json[start..(end + 1)];
        }

        var validNumbers = new HashSet<int>(ctx.Numbers.Select(n => n.Id));
        var result = new List<SuggestedSlot>();
        using var doc = JsonDocument.Parse(json);
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            if (!el.TryGetProperty("date", out var dateEl)
                || !DateOnly.TryParse(dateEl.GetString(), out var date)) continue;
            // Clamp stray dates into the requested range rather than dropping the work.
            if (date < ctx.From) date = ctx.From;
            if (date > ctx.To) date = ctx.To;

            if (!TryTime(el, "startTime", out var start) || !TryTime(el, "endTime", out var end)) continue;
            if (end <= start) end = start.AddHours(1);

            var type = el.TryGetProperty("type", out var typeEl)
                && Enum.TryParse<RehearsalType>(typeEl.GetString(), ignoreCase: true, out var parsed)
                ? parsed : RehearsalType.Other;

            int? numberId = null;
            if (el.TryGetProperty("musicalNumberId", out var numEl)
                && numEl.ValueKind == JsonValueKind.Number
                && validNumbers.Contains(numEl.GetInt32()))
                numberId = numEl.GetInt32();

            var notes = el.TryGetProperty("notes", out var notesEl) && notesEl.ValueKind == JsonValueKind.String
                ? notesEl.GetString() : null;

            result.Add(new SuggestedSlot(
                date.ToString("yyyy-MM-dd"), start.ToString("HH:mm"), end.ToString("HH:mm"),
                type.ToString(), numberId, string.IsNullOrWhiteSpace(notes) ? null : notes));
        }
        return result;
    }

    private static bool TryTime(JsonElement el, string prop, out TimeOnly time)
    {
        time = default;
        return el.TryGetProperty(prop, out var v)
            && v.ValueKind == JsonValueKind.String
            && TimeOnly.TryParse(v.GetString(), out time);
    }
}
