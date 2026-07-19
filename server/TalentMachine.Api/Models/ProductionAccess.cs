namespace TalentMachine.Api.Models;

/// <summary>
/// Grants a Member access to one production (show-level collaboration: a
/// choreographer or music director joins for a specific show, not the whole
/// company). Owners are never restricted and need no rows here. Composite key
/// (MembershipId, ProductionId) — no store-generated Id.
/// </summary>
public class ProductionAccess : ITenantScoped
{
    public int TenantId { get; set; }
    public int MembershipId { get; set; }
    public int ProductionId { get; set; }

    public Membership? Membership { get; set; }
    public Production? Production { get; set; }
}
