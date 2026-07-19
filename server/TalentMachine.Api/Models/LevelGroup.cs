namespace TalentMachine.Api.Models;

/// <summary>
/// An age/ability grouping of a production's kids ("Gold Group", "Intermediate
/// 10–12"). Separate from CastGroup: a kid has a cast group (casting structure)
/// AND a level group (age/ability) per show. Per-production by design — kids
/// improve between years, so each show regroups them; history falls out of
/// CastMembership rows. Age range and color are optional visibility aids and are
/// never enforced.
/// </summary>
public class LevelGroup : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int ProductionId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Free-text ability label, e.g. "Advanced".</summary>
    public string? Level { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    /// <summary>Hex color (e.g. "#EAB308") used for at-a-glance highlighting.</summary>
    public string? Color { get; set; }
    public string? Notes { get; set; }
    public int OrderIndex { get; set; }
}
