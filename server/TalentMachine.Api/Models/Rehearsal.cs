namespace TalentMachine.Api.Models;

/// <summary>
/// One rehearsal slot: a timed block on a date working one number (or a general
/// session when MusicalNumberId is null). The UI groups slots into days/weeks.
/// Attendees default to the number's cast, adjusted by <see cref="RehearsalAttendee"/>
/// override rows.
/// </summary>
public class Rehearsal : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int ProductionId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public RehearsalType Type { get; set; } = RehearsalType.Dance;
    /// <summary>Null = general session (run-through, notes, etc.) — attendees are
    /// then only the explicitly added ones.</summary>
    public int? MusicalNumberId { get; set; }
    public string? Notes { get; set; }

    public Production? Production { get; set; }
    public MusicalNumber? MusicalNumber { get; set; }
}
