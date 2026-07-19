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

    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, AppDbContext db, ICurrentTenant currentTenant)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var clerkUserId = context.User.FindFirst("sub")?.Value
                ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(clerkUserId))
            {
                // Membership is a global table (not tenant-scoped) so this lookup
                // is not subject to the tenant query filter.
                var membership = await db.Memberships
                    .FirstOrDefaultAsync(m => m.ClerkUserId == clerkUserId);

                if (membership is null)
                {
                    var tenant = new Tenant { Name = DeriveTenantName(context.User) };
                    db.Tenants.Add(tenant);
                    await db.SaveChangesAsync();

                    membership = new Membership
                    {
                        TenantId = tenant.Id,
                        ClerkUserId = clerkUserId,
                        Role = MembershipRole.Owner,
                        Email = FindEmail(context.User),
                        DisplayName = context.User.FindFirst("name")?.Value,
                    };
                    db.Memberships.Add(membership);
                    await db.SaveChangesAsync();
                }
                else if (membership.Email is null)
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
                currentTenant.Role = membership.Role;
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
