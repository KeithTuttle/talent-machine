namespace TalentMachine.Api.Models;

/// <summary>
/// A performer's "in the show" record: membership of a production, optionally
/// placed in a cast group. One row per (production, performer) — enforced by a
/// unique index. Surrogate Id (not a composite key) so group moves are a simple
/// PUT and the TenantCrud helpers apply.
/// </summary>
public class CastMembership : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int ProductionId { get; set; }
    public int PerformerId { get; set; }
    /// <summary>Null = in the show but not yet placed in a group.</summary>
    public int? CastGroupId { get; set; }

    public Performer? Performer { get; set; }
    public CastGroup? CastGroup { get; set; }
}
