using System.Net;
using System.Text;
using System.Text.Json;

namespace TalentMachine.Api.Services;

/// <summary>One performer to place, as supplied by the client.</summary>
public record FormationDancer(int PerformerId, string? Gender, string? FirstName);

/// <summary>A stage position in 0–100 percentages.</summary>
public record FormationCoord(double X, double Y);

/// <summary>
/// Result of an AI formation suggestion. <c>Configured</c> is false when no Gemini
/// key is set; <c>Ok</c> is false when the call failed (the client then falls back
/// to a local template). Coordinates are keyed by performerId.
/// </summary>
public record FormationSuggestion(bool Configured, bool Ok, Dictionary<int, FormationCoord> Coordinates);

/// <summary>
/// Suggests a stage formation via Google Gemini's free tier — the same service
/// as dance-manager's FormationAiService (server-side key, structured JSON out,
/// output always validated/repaired so a garbled response can never break the
/// stage, and self-healing model discovery on a 404).
/// </summary>
public class FormationAiService
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<FormationAiService> _logger;
    private readonly string? _apiKey;
    private readonly string _configuredModel;

    private static string? _resolvedModel;
    private static readonly SemaphoreSlim _modelLock = new(1, 1);

    private const double Margin = 6;
    private const double MinGap = 11;

    public FormationAiService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<FormationAiService> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
        _apiKey = config["Gemini:ApiKey"];
        _configuredModel = string.IsNullOrWhiteSpace(config["Gemini:Model"]) ? "gemini-flash-lite-latest" : config["Gemini:Model"]!;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<FormationSuggestion> SuggestAsync(
        IReadOnlyList<FormationDancer> dancers, string? description, CancellationToken ct = default)
    {
        if (dancers.Count == 0)
            return new FormationSuggestion(IsConfigured, true, new());
        if (!IsConfigured)
            return new FormationSuggestion(false, false, new());

        try
        {
            var raw = await CallGeminiAsync(dancers, description, ct);
            var coords = ParseCoords(raw, dancers);
            Repair(coords, dancers);
            return new FormationSuggestion(true, true, coords);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini formation suggestion failed");
            return new FormationSuggestion(true, false, new());
        }
    }

    private async Task<string> CallGeminiAsync(
        IReadOnlyList<FormationDancer> dancers, string? description, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            contents = new[] { new { parts = new[] { new { text = BuildPrompt(dancers, description) } } } },
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
                            performerId = new { type = "INTEGER" },
                            x = new { type = "NUMBER" },
                            y = new { type = "NUMBER" },
                        },
                        required = new[] { "performerId", "x", "y" },
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

    private async Task<HttpResponseMessage> GenerateAsync(HttpClient client, string model, string payload, CancellationToken ct)
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

    private static string BuildPrompt(IReadOnlyList<FormationDancer> dancers, string? description)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You arrange performers on a stage into a formation.");
        sb.AppendLine("Coordinates are percentages from 0 to 100 on a rectangular stage:");
        sb.AppendLine("- x: 0 = stage-left edge, 100 = stage-right edge, 50 = center.");
        sb.AppendLine("- y: 0 = upstage (back, farthest from the audience), 100 = downstage (front, closest to the audience).");
        sb.AppendLine("Rules:");
        sb.AppendLine("- Give every performer an (x, y).");
        sb.AppendLine("- Keep everyone within x 6–94 and y 6–94.");
        sb.AppendLine("- Space performers apart so none overlap (at least ~12 units between any two).");
        sb.AppendLine("- Make it balanced and roughly symmetric about x=50 unless the description says otherwise.");
        sb.AppendLine("- Shape the formation to fit the described number's style/mood.");
        sb.AppendLine();
        sb.AppendLine("Performers:");
        foreach (var d in dancers)
        {
            var g = d.Gender == "Male" ? "boy" : d.Gender == "Female" ? "girl" : "performer";
            var name = string.IsNullOrWhiteSpace(d.FirstName) ? $"#{d.PerformerId}" : d.FirstName;
            sb.AppendLine($"- performerId {d.PerformerId}: {g}, {name}");
        }
        sb.AppendLine();
        sb.Append("Choreographer's description: ");
        sb.AppendLine(string.IsNullOrWhiteSpace(description) ? "a balanced general formation." : description!.Trim());
        sb.AppendLine();
        sb.AppendLine("Return one entry per performer.");
        return sb.ToString();
    }

    private static Dictionary<int, FormationCoord> ParseCoords(string raw, IReadOnlyList<FormationDancer> dancers)
    {
        var json = raw.Trim();
        if (!json.StartsWith('['))
        {
            var start = json.IndexOf('[');
            var end = json.LastIndexOf(']');
            if (start >= 0 && end > start) json = json[start..(end + 1)];
        }

        var valid = new HashSet<int>(dancers.Select(d => d.PerformerId));
        var result = new Dictionary<int, FormationCoord>();
        using var doc = JsonDocument.Parse(json);
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            if (!el.TryGetProperty("performerId", out var idEl)) continue;
            var id = idEl.GetInt32();
            if (!valid.Contains(id)) continue;
            var x = el.TryGetProperty("x", out var xe) ? xe.GetDouble() : 50;
            var y = el.TryGetProperty("y", out var ye) ? ye.GetDouble() : 50;
            result[id] = new FormationCoord(x, y);
        }
        return result;
    }

    private static void Repair(Dictionary<int, FormationCoord> coords, IReadOnlyList<FormationDancer> dancers)
    {
        var missing = dancers.Where(d => !coords.ContainsKey(d.PerformerId)).ToList();
        if (missing.Count > 0)
        {
            var spread = SpreadGrid(missing.Count);
            for (var i = 0; i < missing.Count; i++)
                coords[missing[i].PerformerId] = spread[i];
        }

        var ids = dancers.Select(d => d.PerformerId).Where(coords.ContainsKey).ToList();
        var pts = ids.Select(id => coords[id]).Select(c => new double[] { c.X, c.Y }).ToArray();

        void Clamp()
        {
            foreach (var p in pts)
            {
                p[0] = Math.Clamp(p[0], Margin, 100 - Margin);
                p[1] = Math.Clamp(p[1], Margin, 100 - Margin);
            }
        }
        Clamp();

        for (var pass = 0; pass < 12; pass++)
        {
            var moved = false;
            for (var a = 0; a < pts.Length; a++)
                for (var b = a + 1; b < pts.Length; b++)
                {
                    var dx = pts[a][0] - pts[b][0];
                    var dy = pts[a][1] - pts[b][1];
                    var dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist >= MinGap) continue;
                    moved = true;
                    if (dist < 0.001) { dx = (a % 3) - 1; dy = (b % 3) - 1; dist = Math.Max(0.001, Math.Sqrt(dx * dx + dy * dy)); }
                    var push = (MinGap - dist) / 2 + 0.5;
                    var ux = dx / dist; var uy = dy / dist;
                    pts[a][0] += ux * push; pts[a][1] += uy * push;
                    pts[b][0] -= ux * push; pts[b][1] -= uy * push;
                }
            Clamp();
            if (!moved) break;
        }

        for (var i = 0; i < ids.Count; i++)
            coords[ids[i]] = new FormationCoord(Math.Round(pts[i][0], 1), Math.Round(pts[i][1], 1));
    }

    private static List<FormationCoord> SpreadGrid(int n)
    {
        var cols = (int)Math.Ceiling(Math.Sqrt(n));
        var rows = (int)Math.Ceiling((double)n / cols);
        var list = new List<FormationCoord>(n);
        for (var i = 0; i < n; i++)
        {
            var r = i / cols;
            var c = i % cols;
            var x = cols == 1 ? 50 : 15 + c * (70.0 / (cols - 1));
            var y = rows == 1 ? 50 : 15 + r * (70.0 / (rows - 1));
            list.Add(new FormationCoord(x, y));
        }
        return list;
    }
}
