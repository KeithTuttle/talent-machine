namespace TalentMachine.Api.Models;

/// <summary>
/// Per-slot attendee override. Rows exist only for deviations from the slot's
/// number cast: IsExcluded=false adds an extra kid, IsExcluded=true pulls one
/// out ("leads only"). Resolved attendees = (number cast ∪ added) − excluded.
/// Composite key — no store-generated Id.
/// </summary>
public class RehearsalAttendee : ITenantScoped
{
    public int TenantId { get; set; }
    public int RehearsalId { get; set; }
    public int PerformerId { get; set; }
    public bool IsExcluded { get; set; }

    public Rehearsal? Rehearsal { get; set; }
    public Performer? Performer { get; set; }
}
