using System.Text;
using System.Text.Json;

namespace TalentMachine.Api.Services;

/// <summary>One performer the model pulled out of a messy roster sheet (validated/repaired).</summary>
public record AiCastRow(
    string FirstName, string LastName, string Gender, string DateOfBirth, int? Age,
    string Notes, string GuardianName, string GuardianEmail, string GuardianPhone, string CastGroup);

public record AiCastResult(bool Configured, bool Ok, List<AiCastRow> Rows);

/// <summary>
/// Uses Gemini (via the shared <see cref="GeminiClient"/>) to turn a messy cast
/// roster spreadsheet into structured performers: splits full names, parses gender
/// and age/DOB, and pulls guardian contact, group, and notes. Read-only — proposals
/// are reviewed by the user before anything is imported.
/// </summary>
public class CastImportAiService
{
    private readonly GeminiClient _gemini;
    private readonly ILogger<CastImportAiService> _logger;

    public CastImportAiService(GeminiClient gemini, ILogger<CastImportAiService> logger)
    {
        _gemini = gemini;
        _logger = logger;
    }

    public bool IsConfigured => _gemini.IsConfigured;

    private static readonly object ResponseSchema = new
    {
        type = "ARRAY",
        items = new
        {
            type = "OBJECT",
            properties = new
            {
                firstName = new { type = "STRING" },
                lastName = new { type = "STRING" },
                gender = new { type = "STRING" },
                dateOfBirth = new { type = "STRING" },
                age = new { type = "INTEGER" },
                notes = new { type = "STRING" },
                guardianName = new { type = "STRING" },
                guardianEmail = new { type = "STRING" },
                guardianPhone = new { type = "STRING" },
                castGroup = new { type = "STRING" },
            },
            // All required — flash-lite otherwise satisfies the schema minimally and drops fields.
            required = new[]
            {
                "firstName", "lastName", "gender", "dateOfBirth", "age",
                "notes", "guardianName", "guardianEmail", "guardianPhone", "castGroup",
            },
        },
    };

    public async Task<AiCastResult> ExtractAsync(List<List<string>> rows, CancellationToken ct = default)
    {
        if (!IsConfigured) return new AiCastResult(false, false, new());
        try
        {
            var raw = await _gemini.GenerateJsonAsync(BuildPrompt(rows), ResponseSchema, ct);
            return new AiCastResult(true, true, ParseAndRepair(raw));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini cast-import extraction failed");
            return new AiCastResult(true, false, new());
        }
    }

    private static string BuildPrompt(List<List<string>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You extract a cast roster from a messy spreadsheet for a youth musical theater production.");
        sb.AppendLine("The sheet may have arbitrary column order, a header row, freeform text, and various formats.");
        sb.AppendLine();
        sb.AppendLine("CRITICAL: Each DATA row below is ONE performer. Produce exactly one output object per data row.");
        sb.AppendLine("Skip a header row (labels like \"name\", \"age\", \"parent\") and skip empty rows. Do NOT invent people.");
        sb.AppendLine();
        sb.AppendLine("For each object:");
        sb.AppendLine("- firstName / lastName: split a full name (e.g. \"Ava Brown\"→\"Ava\",\"Brown\"; \"Brown, Ava\"→\"Ava\",\"Brown\").");
        sb.AppendLine("- gender: \"Male\", \"Female\", or \"NonBinary\" (map M/F/boy/girl/nb/x; \"\" if unknown).");
        sb.AppendLine($"- dateOfBirth: yyyy-MM-dd if a birthdate is given (parse informal dates), else \"\".");
        sb.AppendLine("- age: the age in years if given as a number, else 0.");
        sb.AppendLine("- notes: any note about the performer, else \"\".");
        sb.AppendLine("- guardianName / guardianEmail / guardianPhone: the parent/guardian contact if present, else \"\".");
        sb.AppendLine("- castGroup: a group/cast name if present (e.g. \"Gold\", \"Ensemble\"), else \"\".");
        sb.AppendLine();
        sb.AppendLine("Format example (illustrative):");
        sb.AppendLine("  \"Row 2: Ava Brown | girl | 9 | mom Linda lbrown@ex.com 410-555-0130 | Gold | strong belter\"  →");
        sb.AppendLine("  {\"firstName\":\"Ava\",\"lastName\":\"Brown\",\"gender\":\"Female\",\"dateOfBirth\":\"\",\"age\":9,\"notes\":\"strong belter\",\"guardianName\":\"Linda\",\"guardianEmail\":\"lbrown@ex.com\",\"guardianPhone\":\"410-555-0130\",\"castGroup\":\"Gold\"}");
        sb.AppendLine();
        sb.AppendLine("Data — process each row independently:");
        for (var i = 0; i < rows.Count && i < 300; i++)
            sb.AppendLine($"Row {i + 1}: {string.Join(" | ", rows[i])}");
        sb.AppendLine();
        sb.AppendLine("Return one JSON object per performer row (not the header).");
        return sb.ToString();
    }

    private static List<AiCastRow> ParseAndRepair(string raw)
    {
        var json = raw.Trim();
        if (!json.StartsWith('['))
        {
            var start = json.IndexOf('[');
            var end = json.LastIndexOf(']');
            if (start >= 0 && end > start) json = json[start..(end + 1)];
        }

        var result = new List<AiCastRow>();
        using var doc = JsonDocument.Parse(json);
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var first = Str(el, "firstName");
            var last = Str(el, "lastName");
            if (string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(last)) continue;

            var gender = Str(el, "gender") switch
            {
                var g when g.Equals("Male", StringComparison.OrdinalIgnoreCase) => "Male",
                var g when g.Equals("Female", StringComparison.OrdinalIgnoreCase) => "Female",
                var g when g.Equals("NonBinary", StringComparison.OrdinalIgnoreCase) => "NonBinary",
                _ => "",
            };
            var dob = DateOnly.TryParse(Str(el, "dateOfBirth"), out var d) ? d.ToString("yyyy-MM-dd") : "";
            int? age = el.TryGetProperty("age", out var a) && a.ValueKind == JsonValueKind.Number
                && a.TryGetInt32(out var ai) && ai is > 0 and < 120 ? ai : null;

            result.Add(new AiCastRow(
                first.Trim(), last.Trim(), gender, dob, age,
                Str(el, "notes").Trim(), Str(el, "guardianName").Trim(),
                Str(el, "guardianEmail").Trim(), Str(el, "guardianPhone").Trim(), Str(el, "castGroup").Trim()));
        }
        return result;
    }

    private static string Str(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
