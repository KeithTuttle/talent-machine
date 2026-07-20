namespace TalentMachine.Api.Models;

/// <summary>
/// A non-performer person in the company directory — directors, choreographers,
/// music directors, producers, and anyone invited for login access. Tenant-level
/// so returning people are reused across shows and years (see
/// <see cref="ProductionStaff"/> for per-show creative assignments).
/// </summary>
public class StaffMember : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
}
