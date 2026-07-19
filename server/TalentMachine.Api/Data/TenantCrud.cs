using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Data;

/// <summary>
/// Tenant-safe by-id lookups/mutations. Unlike <c>FindAsync</c> (a keyed lookup
/// that bypasses global query filters), these go through a filtered query, so a
/// row belonging to another tenant simply isn't found — a caller can't read,
/// update, or delete data outside their tenant by guessing an id.
/// </summary>
public static class TenantCrud
{
    /// <summary>Find an entity by id, scoped to the current tenant (null if not owned).</summary>
    public static Task<T?> FindScopedAsync<T>(this AppDbContext db, int id) where T : class
        => db.Set<T>().FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id);

    /// <summary>
    /// Update an entity by id, scoped to the current tenant. Copies scalar values
    /// from <paramref name="input"/> but preserves the row's existing TenantId, so
    /// a client can neither move a row across tenants nor edit another tenant's row.
    /// Returns false (→ NotFound) if the id isn't owned by the current tenant.
    /// </summary>
    public static async Task<bool> UpdateScopedAsync<T>(this AppDbContext db, int id, T input)
        where T : class, ITenantScoped
    {
        var existing = await db.Set<T>().FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id);
        if (existing is null) return false;
        var tenantId = existing.TenantId;
        db.Entry(existing).CurrentValues.SetValues(input);
        existing.TenantId = tenantId;
        await db.SaveChangesAsync();
        return true;
    }
}
