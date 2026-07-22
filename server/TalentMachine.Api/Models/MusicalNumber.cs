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
    public string? Notes { get; set; }
    /// <summary>Who choreographs this number (from the staff directory); null = unassigned.</summary>
    public int? ChoreographerStaffId { get; set; }
    /// <summary>How far along teaching is; null = not taught yet.</summary>
    public TeachStatus? TeachStatus { get; set; }
    /// <summary>Free-text costume label; same non-empty label on adjacent numbers = no quick change.</summary>
    public string? CostumeLabel { get; set; }
    /// <summary>Which act this number belongs to; null = not yet in the running order.</summary>
    public int? ActId { get; set; }
    /// <summary>Which scene this number sits inside; null = not nested under a scene.</summary>
    public int? SceneId { get; set; }
    public int OrderIndex { get; set; }

    public Act? Act { get; set; }
    public Scene? Scene { get; set; }
    public StaffMember? Choreographer { get; set; }
}
