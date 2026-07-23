namespace TalentMachine.Api.Models;

/// <summary>
/// A stage prop in a production's catalog — reusable across scenes (a broom, a
/// locket, a radio). Its per-scene usage (preset location, who carries it on,
/// where it strikes) lives on <see cref="PropAssignment"/>. The catalog fields
/// here drive the pre-show "pull list": quantity, where it's stored, and how far
/// along gathering it is.
/// </summary>
public class Prop : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int ProductionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>How many are needed (e.g. 6 scrub brushes). Default 1.</summary>
    public int Quantity { get; set; } = 1;
    /// <summary>Where the prop lives when not on stage (a bin, a shelf, borrowed-from).</summary>
    public string? StorageLocation { get; set; }
    public PropStatus Status { get; set; } = PropStatus.Needed;
    public string? Notes { get; set; }
    public int OrderIndex { get; set; }
}
