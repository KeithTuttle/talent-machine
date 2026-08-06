using TalentMachine.Api.Models;

namespace TalentMachine.Api.Services;

/// <summary>
/// One performer changing costume between two of the numbers they're in.
/// <paramref name="Buffer"/> is how many numbers of the running order sit between
/// them: 0 = back-to-back (a true quick change), 1 = tight, 2+ = time to spare.
/// </summary>
public record CostumeChange(
    int PerformerId,
    MusicalNumber From,
    MusicalNumber To,
    string FromCostume,
    string ToCostume,
    int Buffer);

/// <summary>
/// Works out every costume change in a production, per performer, from their
/// assigned looks (falling back to the number's costume label). Consecutive
/// APPEARANCES are compared — not just adjacent numbers — so a kid who changes
/// with a number's breathing room in between is still caught, just ranked calmer.
/// An act break between the two means an intermission, so it never counts.
///
/// The client mirrors this in CastOverviewGrid.vue for the live panel; keep the
/// two in step.
/// </summary>
public static class CostumeChanges
{
    /// <summary>Numbers in running order: each act in turn, then anything unplaced.</summary>
    public static List<MusicalNumber> RunningOrder(List<Act> acts, List<MusicalNumber> numbers)
    {
        var ordered = new List<MusicalNumber>();
        foreach (var act in acts.OrderBy(a => a.OrderIndex).ThenBy(a => a.Id))
            ordered.AddRange(numbers.Where(n => n.ActId == act.Id).OrderBy(n => n.OrderIndex).ThenBy(n => n.Id));
        ordered.AddRange(numbers.Where(n => n.ActId == null).OrderBy(n => n.OrderIndex).ThenBy(n => n.Id));
        return ordered;
    }

    /// <summary>Display name for a costume: its look name, else its description.</summary>
    public static string LookName(CostumePiece p) =>
        !string.IsNullOrWhiteSpace(p.Label) ? p.Label!.Trim()
        : !string.IsNullOrWhiteSpace(p.Description) ? p.Description!.Trim()
        : p.Gender != CostumeGender.All ? p.Gender.ToString()
        : "Costume";

    /// <summary>
    /// What a performer wears in a number: the look they're assigned, else the
    /// number's costume label. Empty = unknown, which never counts as a change.
    /// </summary>
    public static string Identity(
        MusicalNumber number, int performerId,
        Dictionary<(int, int), CostumeAssignment> assignments,
        Dictionary<int, CostumePiece> pieces)
    {
        if (assignments.TryGetValue((number.Id, performerId), out var a)
            && a.CostumePieceId is int pieceId
            && pieces.TryGetValue(pieceId, out var piece))
        {
            var look = !string.IsNullOrWhiteSpace(piece.Label) ? piece.Label : piece.Description;
            if (!string.IsNullOrWhiteSpace(look)) return look!.Trim();
        }
        return number.CostumeLabel?.Trim() ?? string.Empty;
    }

    public static List<CostumeChange> Detect(
        List<Act> acts, List<MusicalNumber> numbers, List<NumberCast> casts,
        List<CostumeAssignment> assignments, List<CostumePiece> pieces)
    {
        var ordered = RunningOrder(acts, numbers);
        var position = new Dictionary<int, int>();
        for (var i = 0; i < ordered.Count; i++) position[ordered[i].Id] = i;

        var pieceById = pieces.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
        var assignmentByKey = assignments
            .GroupBy(a => (a.MusicalNumberId, a.PerformerId))
            .ToDictionary(g => g.Key, g => g.First());

        var changes = new List<CostumeChange>();
        foreach (var group in casts.ToLookup(c => c.PerformerId, c => c.MusicalNumberId))
        {
            var appearances = group
                .Where(position.ContainsKey)
                .Distinct()
                .OrderBy(id => position[id])
                .Select(id => ordered[position[id]])
                .ToList();

            for (var i = 0; i < appearances.Count - 1; i++)
            {
                var from = appearances[i];
                var to = appearances[i + 1];
                // An act break sits between them → intermission, never a rush.
                if (from.ActId is not null && to.ActId is not null && from.ActId != to.ActId) continue;

                var a = Identity(from, group.Key, assignmentByKey, pieceById);
                var b = Identity(to, group.Key, assignmentByKey, pieceById);
                if (a.Length == 0 || b.Length == 0) continue;
                if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) continue;

                changes.Add(new CostumeChange(
                    group.Key, from, to, a, b, position[to.Id] - position[from.Id] - 1));
            }
        }

        return changes
            .OrderBy(c => c.Buffer)
            .ThenBy(c => position[c.From.Id])
            .ToList();
    }
}
