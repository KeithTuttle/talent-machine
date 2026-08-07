namespace TalentMachine.Api.Models;

/// <summary>
/// A costume in a production's catalog — reusable across numbers (the orphan
/// rags, the bear suit), so its description, vendor link and photo are entered
/// once no matter how many numbers wear it. Which numbers use it lives on
/// <see cref="CostumeNumber"/>; who wears it, in what size, and whether they've
/// been fitted lives on <see cref="CostumeAssignment"/>.
///
/// Mirrors <see cref="Prop"/> — catalog entry + per-use join.
/// </summary>
public class Costume : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int ProductionId { get; set; }
    /// <summary>Short name, e.g. "Orphan rags", "Bear". Identity for reuse.</summary>
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Accessories { get; set; }
    public string? Shoes { get; set; }
    public string? PhotoUrl { get; set; }
    public string? VendorUrl { get; set; }
    public CostumeStatus Status { get; set; } = CostumeStatus.Needed;
    public string? Notes { get; set; }
    public int OrderIndex { get; set; }
}
