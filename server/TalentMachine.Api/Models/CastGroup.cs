namespace TalentMachine.Api.Models;

/// <summary>
/// A named grouping of a production's cast ("Leads", "Ensemble", "Kids Chorus").
/// Per-production by design: a new show starts with fresh groups, keeping each
/// year's history exact.
/// </summary>
public class CastGroup : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int ProductionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
}
