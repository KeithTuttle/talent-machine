namespace TalentMachine.Api.Models;

/// <summary>
/// An invitation for another Clerk user (a co-director / choreographer) to join
/// this tenant. Flow: an Owner creates an invite and shares its short
/// <see cref="Code"/>; the invitee signs up normally (auto-provisioning their own
/// empty tenant), then redeems the code, which MOVES their membership into this
/// tenant. Code-based rather than email-based because Clerk session JWTs don't
/// reliably carry an email claim.
/// </summary>
public class Invitation : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }

    /// <summary>Short join code the invitee enters (unique, unambiguous alphabet).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Optional note of who this invite is for (informational only).</summary>
    public string? Email { get; set; }

    public MembershipRole Role { get; set; } = MembershipRole.Member;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Set when redeemed; a redeemed invite cannot be used again.</summary>
    public DateTimeOffset? AcceptedAt { get; set; }
    public string? AcceptedByClerkUserId { get; set; }
}
