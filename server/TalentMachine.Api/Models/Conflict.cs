namespace TalentMachine.Api.Models;

/// <summary>
/// A performer's scheduling conflict within one production — the core of
/// rehearsal planning. Two shapes:
/// <list type="bullet">
/// <item>OneOff — unavailable StartDate through EndDate (null EndDate = single day).</item>
/// <item>Weekly — unavailable every <see cref="Weekday"/> from StartDate through
/// EndDate (null = open-ended), e.g. "every Tuesday" for a standing dance class.</item>
/// </list>
/// </summary>
public class Conflict : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int ProductionId { get; set; }
    public int PerformerId { get; set; }
    public ConflictType Type { get; set; } = ConflictType.OneOff;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    /// <summary>Weekly conflicts only.</summary>
    public DayOfWeek? Weekday { get; set; }
    public string? Reason { get; set; }

    public Production? Production { get; set; }
    public Performer? Performer { get; set; }
}
