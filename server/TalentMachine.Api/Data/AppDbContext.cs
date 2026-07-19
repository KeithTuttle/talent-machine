using System.Reflection;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Data;

public class AppDbContext : DbContext
{
    private readonly ICurrentTenant _tenant;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenant tenant) : base(options)
        => _tenant = tenant;

    // Tenancy tables (global — NOT tenant-scoped).
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Membership> Memberships => Set<Membership>();

    // Team invites (tenant-scoped; redemption looks up by code with IgnoreQueryFilters).
    public DbSet<Invitation> Invitations => Set<Invitation>();

    // Domain.
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Production> Productions => Set<Production>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<CastGroup> CastGroups => Set<CastGroup>();
    public DbSet<CastMembership> CastMemberships => Set<CastMembership>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<MusicalNumber> Numbers => Set<MusicalNumber>();
    public DbSet<NumberCast> NumberCasts => Set<NumberCast>();

    /// <summary>Tenant used by the query filter; 0 (matches nothing) when unresolved.</summary>
    private int CurrentTenantId => _tenant.TenantId ?? 0;

    private static readonly MethodInfo ConfigureTenantFilterMethod = typeof(AppDbContext)
        .GetMethod(nameof(ConfigureTenantScope), BindingFlags.Instance | BindingFlags.NonPublic)!;

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Store enums as strings for readability in the database.
        b.Entity<Membership>().Property(x => x.Role).HasConversion<string>();
        b.Entity<Invitation>().Property(x => x.Role).HasConversion<string>();

        // Composite keys (join rows without a store-generated Id).
        b.Entity<NumberCast>().HasKey(x => new { x.MusicalNumberId, x.PersonId });

        // One Clerk user maps to exactly one membership.
        b.Entity<Membership>().HasIndex(x => x.ClerkUserId).IsUnique();
        // Join codes are redeemed by exact lookup and must be unique.
        b.Entity<Invitation>().HasIndex(x => x.Code).IsUnique();

        // A person is in a production at most once.
        b.Entity<CastMembership>().HasIndex(x => new { x.ProductionId, x.PersonId }).IsUnique();

        // Deleting a cast group must NOT drop people from the show — their
        // membership falls back to "ungrouped" (CastGroupId null).
        b.Entity<CastMembership>()
            .HasOne(x => x.CastGroup).WithMany().HasForeignKey(x => x.CastGroupId)
            .OnDelete(DeleteBehavior.SetNull);
        // Deleting a person removes their production memberships and number casts.
        b.Entity<CastMembership>()
            .HasOne(x => x.Person).WithMany().HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Cascade);
        // Deleting a person leaves their roles behind as "uncast" characters.
        b.Entity<Role>()
            .HasOne(x => x.Person).WithMany().HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.SetNull);

        // Helpful lookup indexes.
        b.Entity<Season>().HasIndex(x => new { x.TenantId, x.Year });
        b.Entity<MusicalNumber>().HasIndex(x => new { x.ProductionId, x.OrderIndex });

        // Tenant isolation: every ITenantScoped entity gets a global query filter
        // (e.TenantId == current tenant) and a TenantId index. Defense-in-depth —
        // a query that forgets to filter still cannot cross tenants.
        foreach (var entityType in b.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
                ConfigureTenantFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, new object[] { b });
        }
    }

    private void ConfigureTenantScope<T>(ModelBuilder b) where T : class, ITenantScoped
    {
        b.Entity<T>().HasIndex(e => e.TenantId);
        // References the context instance member so EF re-evaluates it per query
        // rather than baking a constant into the cached model.
        b.Entity<T>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTenant();
        ResetStoreGeneratedKeys();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTenant();
        ResetStoreGeneratedKeys();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Clears any client-supplied value on a store-generated (identity) primary key
    /// before insert, so the database always assigns the id. Controllers bind whole
    /// entities from request bodies, and the client sends a temporary negative id for
    /// optimistic UI rows; without this, Postgres' <c>IDENTITY BY DEFAULT</c> honors
    /// that value — inserting a negative PK, then failing later inserts with a
    /// duplicate-key (23505) violation. Composite-key join rows (NumberCast) have no
    /// store-generated key and are left untouched.
    /// </summary>
    private void ResetStoreGeneratedKeys()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added) continue;
            var key = entry.Metadata.FindPrimaryKey();
            if (key is null) continue;
            foreach (var prop in key.Properties)
            {
                if (prop.ValueGenerated == Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd
                    && (prop.ClrType == typeof(int) || prop.ClrType == typeof(long)))
                {
                    entry.Property(prop.Name).CurrentValue = Activator.CreateInstance(prop.ClrType);
                }
            }
        }
    }

    /// <summary>Stamp the current tenant onto new tenant-scoped rows on insert.</summary>
    private void StampTenant()
    {
        var tid = _tenant.TenantId;
        if (tid is null or 0) return;
        foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == 0)
                entry.Entity.TenantId = tid.Value;
        }
    }
}
