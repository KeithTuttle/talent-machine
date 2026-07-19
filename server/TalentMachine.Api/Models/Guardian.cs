namespace TalentMachine.Api.Models;

/// <summary>
/// A parent/guardian. Tenant-level (not per-kid) so siblings across shows and
/// years share one record — linked to performers via <see cref="PerformerGuardian"/>.
/// Emails feed the rehearsal-schedule mail flow.
/// </summary>
public class Guardian : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
}
