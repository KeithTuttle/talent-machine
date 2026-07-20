namespace TalentMachine.Api.Models;

/// <summary>
/// One kid's recorded attendance at one rehearsal. Kids whose recorded conflict
/// hits the rehearsal day are auto-marked Excused by the "Mark all" flow; an
/// Absent with no matching conflict is surfaced as "unexcused" in summaries.
/// Composite key — no store-generated Id.
/// </summary>
public class RehearsalAttendance : ITenantScoped
{
    public int TenantId { get; set; }
    public int RehearsalId { get; set; }
    public int PerformerId { get; set; }
    public AttendanceStatus Status { get; set; }

    public Rehearsal? Rehearsal { get; set; }
    public Performer? Performer { get; set; }
}
