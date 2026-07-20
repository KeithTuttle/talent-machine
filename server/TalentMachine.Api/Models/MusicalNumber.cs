namespace TalentMachine.Api.Models;

/// <summary>
/// A musical number in a production — first-class, not hung off a group.
/// Its cast is the set of <see cref="NumberCast"/> rows.
/// </summary>
public class MusicalNumber : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int ProductionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Songwriter { get; set; }
    public string? Notes { get; set; }
    /// <summary>Which act this number belongs to; null = not yet in the running order.</summary>
    public int? ActId { get; set; }
    public int OrderIndex { get; set; }

    public Act? Act { get; set; }
}
