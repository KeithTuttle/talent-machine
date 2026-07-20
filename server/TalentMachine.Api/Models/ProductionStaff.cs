namespace TalentMachine.Api.Models;

/// <summary>
/// A creative-team assignment: a <see cref="StaffMember"/> holding a role on a
/// production. Composite key (ProductionId, StaffMemberId, Role) — one person can
/// hold several roles and a role can have several people. Separate from login
/// access (Membership / ProductionAccess).
/// </summary>
public class ProductionStaff : ITenantScoped
{
    public int TenantId { get; set; }
    public int ProductionId { get; set; }
    public int StaffMemberId { get; set; }
    public StaffRole Role { get; set; }

    public Production? Production { get; set; }
    public StaffMember? StaffMember { get; set; }
}
