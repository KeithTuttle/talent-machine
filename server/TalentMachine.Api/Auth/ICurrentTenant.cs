namespace TalentMachine.Api.Auth;

/// <summary>
/// Per-request holder for the resolved tenant. Populated by
/// <see cref="TenantResolutionMiddleware"/> from the authenticated user, and read
/// by <c>AppDbContext</c> for its global query filter and insert-time stamping.
/// Null until resolved (e.g. anonymous requests / startup) — the query filter
/// treats "no tenant" as "see nothing".
/// </summary>
public interface ICurrentTenant
{
    int? TenantId { get; set; }

    /// <summary>The caller's membership role in the tenant; null when unresolved
    /// (anonymous requests, or auth disabled in local dev).</summary>
    Models.MembershipRole? Role { get; set; }
}

public sealed class CurrentTenant : ICurrentTenant
{
    public int? TenantId { get; set; }
    public Models.MembershipRole? Role { get; set; }
}
