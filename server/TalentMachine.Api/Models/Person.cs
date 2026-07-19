namespace TalentMachine.Api.Models;

/// <summary>
/// A performer. Tenant-level identity — NOT tied to a production — so one
/// person's history spans productions and years (via CastMembership rows).
/// </summary>
public class Person : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
