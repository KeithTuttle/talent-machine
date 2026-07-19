namespace TalentMachine.Api.Models;

/// <summary>
/// Maps a Clerk user (by their Clerk user id, the JWT `sub` claim) to a tenant.
/// The first login for a user auto-creates a tenant + an Owner membership;
/// additional members can be added later (co-directors) without a schema change.
/// </summary>
public class Membership
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    /// <summary>Clerk user id (JWT `sub`). Unique across the system.</summary>
    public string ClerkUserId { get; set; } = string.Empty;
    public MembershipRole Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Display fields captured from Clerk claims when available (claims are
    /// optional on session tokens, so both may stay null).</summary>
    public string? Email { get; set; }
    public string? DisplayName { get; set; }

    public Tenant? Tenant { get; set; }
}
