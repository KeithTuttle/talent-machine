namespace TalentMachine.Api.Models;

/// <summary>
/// A performer. Tenant-level identity — NOT tied to a production — so one
/// performer's history spans productions and years (via CastMembership rows).
/// </summary>
public class Performer : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Gender? Gender { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
