using System.Net;
using System.Text;
using System.Text.Json;

namespace TalentMachine.Api.Services;

/// <summary>
/// Thin shared wrapper over Google Gemini's free tier for structured-JSON calls:
/// server-side key only, a caller-supplied response schema, and self-healing model
/// discovery if the configured model name is ever retired (404 → ListModels). The
/// older AI services (rehearsal, formation, conflict-import) still embed this same
/// plumbing; new services should use this client instead of copying it again.
/// </summary>
public class GeminiClient
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<GeminiClient> _logger;
    private readonly string? _apiKey;
    private readonly string _configuredModel;

    private static string? _resolvedModel;
    private static readonly SemaphoreSlim _modelLock = new(1, 1);

    public GeminiClient(IHttpClientFactory httpFactory, IConfiguration config, ILogger<GeminiClient> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
        _apiKey = config["Gemini:ApiKey"];
        _configuredModel = string.IsNullOrWhiteSpace(config["Gemini:Model"]) ? "gemini-flash-lite-latest" : config["Gemini:Model"]!;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    /// <summary>
    /// Sends <paramref name="prompt"/> asking for JSON shaped by <paramref name="responseSchema"/>
    /// (an anonymous object mirroring Gemini's schema DSL) and returns the raw text part.
    /// Throws on timeout / network / bad key / unparseable output — callers degrade to "not ok".
    /// </summary>
    public async Task<string> GenerateJsonAsync(string prompt, object responseSchema, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { responseMimeType = "application/json", responseSchema },
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
}
