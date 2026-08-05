using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Auth;

/// <summary>
/// For every authenticated request, resolves the caller's tenant from their Clerk
/// user id (the JWT `sub`) and publishes it via <see cref="ICurrentTenant"/> so the
/// DbContext filters/stamps by it. A user's first request auto-provisions a fresh
/// tenant + Owner membership (open self-service signup). Anonymous requests pass
/// through untouched (no tenant → the query filter returns nothing).
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    // Serializes first-login auto-provisioning so concurrent initial requests
    // don't each create a tenant for the same new user.
    private static readonly SemaphoreSlim _provisionGate = new(1, 1);

    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    private static Task<List<Membership>> LoadMembershipsAsync(AppDbContext db, string clerkUserId) =>
        db.Memberships
            .Where(m => m.ClerkUserId == clerkUserId)
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .ToListAsync();

    public async Task InvokeAsync(HttpContext context, AppDbContext db, ICurrentTenant currentTenant)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var clerkUserId = context.User.FindFirst("sub")?.Value
                ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(clerkUserId))
            {
                // Membership is a global table (not tenant-scoped) so these lookups
                // are not subject to the tenant query filter. A user can belong to
                // several companies; the X-Tenant-Id header picks which is active.
                var memberships = await LoadMembershipsAsync(db, clerkUserId);

                if (memberships.Count == 0)
                {
                    // First login → auto-provision one company. Serialize it: on the
                    // first request the client fires several calls at once, and without
                    // this two of them could each create a tenant (they'd be distinct,
                    // so the unique index wouldn't catch it) — leaving a duplicate
                    // "My Company". In-process guard; correct for a single instance.
                    await _provisionGate.WaitAsync();
                    try
                    {
                        memberships = await LoadMembershipsAsync(db, clerkUserId); // re-check under the lock
                        if (memberships.Count == 0)
                        {
                            var tenant = new Tenant { Name = DeriveTenantName(context.User) };
                            db.Tenants.Add(tenant);
                            await db.SaveChangesAsync();

                            var created = new Membership
                            {
                                TenantId = tenant.Id,
                                ClerkUserId = clerkUserId,
                                Role = MembershipRole.Owner,
                                Email = FindEmail(context.User),
                                DisplayName = context.User.FindFirst("name")?.Value,
                            };
                            db.Memberships.Add(created);
                            await db.SaveChangesAsync();
                            memberships = new List<Membership> { created };
                        }
                    }
                    finally
                    {
                        _provisionGate.Release();
                    }
                }

                // Active company = the requested one IF the caller belongs to it;
                // otherwise fall back to their default (oldest). A stale or spoofed
                // header can never resolve to a company they're not a member of.
                int? requested = int.TryParse(context.Request.Headers["X-Tenant-Id"], out var t) ? t : null;
                var membership = (requested is int rid ? memberships.FirstOrDefault(m => m.TenantId == rid) : null)
                    ?? memberships[0];

                if (membership.Email is null)
                {
                    // Backfill display fields when the claim is available and the
                    // field is still empty (claims are optional on session tokens).
                    var email = FindEmail(context.User);
                    if (email is not null)
                    {
                        membership.Email = email;
                        membership.DisplayName ??= context.User.FindFirst("name")?.Value;
                        await db.SaveChangesAsync();
                    }
                }

                currentTenant.TenantId = membership.TenantId;
                currentTenant.MembershipId = membership.Id;
                currentTenant.Role = membership.Role;

                // Members collaborate at the show level: they only see productions
                // they've been granted (ProductionAccess). Owners are unrestricted
                // (AccessibleProductionIds stays null). Set the tenant BEFORE this
                // query so the ProductionAccess tenant filter applies correctly.
                if (membership.Role == MembershipRole.Member)
                {
                    currentTenant.AccessibleProductionIds = await db.ProductionAccesses
                        .Where(a => a.MembershipId == membership.Id)
                        .Select(a => a.ProductionId)
                        .ToListAsync();
                }
            }
        }

        await _next(context);
    }

    private static string DeriveTenantName(System.Security.Claims.ClaimsPrincipal user)
    {
        var name = user.FindFirst("name")?.Value ?? FindEmail(user);
        return string.IsNullOrWhiteSpace(name) ? "My Company" : $"{name}'s Company";
    }

    private static string? FindEmail(System.Security.Claims.ClaimsPrincipal user) =>
        user.FindFirst("email")?.Value
        ?? user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
}
