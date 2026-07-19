using TalentMachine.Api.Models;

namespace TalentMachine.Api.Services;

/// <summary>
/// Shared rules for rehearsal slots, used by the PDF and AI services (the client
/// mirrors both in lib/rehearsals.ts / lib/conflicts.ts).
/// </summary>
public static class RehearsalResolver
{
    /// <summary>Whether a conflict makes its performer unavailable on <paramref name="date"/>.</summary>
    public static bool ConflictOccursOn(Conflict c, DateOnly date)
    {
        if (c.Type == ConflictType.Weekly)
        {
            if (c.Weekday is null || date.DayOfWeek != c.Weekday) return false;
            if (date < c.StartDate) return false;
            return c.EndDate is null || date <= c.EndDate;
        }
        var end = c.EndDate ?? c.StartDate;
        return date >= c.StartDate && date <= end;
    }

    /// <summary>
    /// Resolved attendees: (number cast ∪ added) − excluded; number-less slots
    /// use only explicitly added rows.
    /// </summary>
    public static HashSet<int> ResolveAttendees(
        Rehearsal slot,
        ILookup<int, int> castByNumber,
        IEnumerable<RehearsalAttendee> overrides)
    {
        var result = slot.MusicalNumberId is int numberId
            ? new HashSet<int>(castByNumber[numberId])
            : new HashSet<int>();
        foreach (var o in overrides)
        {
            if (o.RehearsalId != slot.Id) continue;
            if (o.IsExcluded) result.Remove(o.PerformerId);
            else result.Add(o.PerformerId);
        }
        return result;
    }
}
