namespace TalentMachine.Api.Models;

/// <summary>
/// An isolated account (one production company / creative team). All domain data
/// is scoped to a tenant. A tenant may have multiple member logins (see
/// <see cref="Membership"/>); a new signup is auto-provisioned a solo tenant.
/// </summary>
public class Tenant
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Membership> Memberships { get; set; } = new List<Membership>();
}
