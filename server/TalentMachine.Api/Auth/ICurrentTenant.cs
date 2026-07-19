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

    /// <summary>The caller's membership id (global table); null when unresolved.</summary>
    int? MembershipId { get; set; }

    /// <summary>The caller's membership role in the tenant; null when unresolved
    /// (anonymous requests, or auth disabled in local dev).</summary>
    Models.MembershipRole? Role { get; set; }

    /// <summary>
    /// Productions this caller may see/edit. Null = unrestricted (Owner, or auth
    /// disabled in local dev). Members get the ids from their ProductionAccess
    /// rows — show-level collaborators (a choreographer on one show) see only
    /// their shows, while the company Owner sees everything.
    /// </summary>
    IReadOnlyList<int>? AccessibleProductionIds { get; set; }
}

public sealed class CurrentTenant : ICurrentTenant
{
    public int? TenantId { get; set; }
    public int? MembershipId { get; set; }
    public Models.MembershipRole? Role { get; set; }
    public IReadOnlyList<int>? AccessibleProductionIds { get; set; }
}

public static class CurrentTenantExtensions
{
    /// <summary>Owner when resolved; also true when auth is disabled in local dev
    /// (the middleware never runs, so Role stays null — the whole API is open then).</summary>
    public static bool IsOwner(this ICurrentTenant tenant) =>
        tenant.Role is null or Models.MembershipRole.Owner;

    /// <summary>Whether the caller may touch data belonging to this production.</summary>
    public static bool CanAccessProduction(this ICurrentTenant tenant, int productionId) =>
        tenant.AccessibleProductionIds is null
        || tenant.AccessibleProductionIds.Contains(productionId);
}
