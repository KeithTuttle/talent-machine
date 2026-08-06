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

    // Show-level access grants for Members (Owners are unrestricted).
    public DbSet<ProductionAccess> ProductionAccesses => Set<ProductionAccess>();

    // Domain.
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Production> Productions => Set<Production>();
    public DbSet<Performer> Performers => Set<Performer>();
    public DbSet<CastGroup> CastGroups => Set<CastGroup>();
    public DbSet<CastMembership> CastMemberships => Set<CastMembership>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<MusicalNumber> Numbers => Set<MusicalNumber>();
    public DbSet<NumberCast> NumberCasts => Set<NumberCast>();
    public DbSet<Conflict> Conflicts => Set<Conflict>();
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<PerformerGuardian> PerformerGuardians => Set<PerformerGuardian>();
    public DbSet<Rehearsal> Rehearsals => Set<Rehearsal>();
    public DbSet<RehearsalAttendee> RehearsalAttendees => Set<RehearsalAttendee>();
    public DbSet<RehearsalAttendance> RehearsalAttendances => Set<RehearsalAttendance>();
    public DbSet<Act> Acts => Set<Act>();
    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
    public DbSet<ProductionStaff> ProductionStaff => Set<ProductionStaff>();
    public DbSet<CostumePiece> CostumePieces => Set<CostumePiece>();
    public DbSet<CostumeAssignment> CostumeAssignments => Set<CostumeAssignment>();
    public DbSet<Formation> Formations => Set<Formation>();
    public DbSet<Scene> Scenes => Set<Scene>();
    public DbSet<SceneCharacter> SceneCharacters => Set<SceneCharacter>();
    public DbSet<NumberCharacter> NumberCharacters => Set<NumberCharacter>();
    public DbSet<Prop> Props => Set<Prop>();
    public DbSet<PropAssignment> PropAssignments => Set<PropAssignment>();

    /// <summary>Tenant used by the query filter; 0 (matches nothing) when unresolved.</summary>
    private int CurrentTenantId => _tenant.TenantId ?? 0;

    // Unique placeholders for cleared client-supplied keys (see ResetStoreGeneratedKeys).
    // Far below EF's own temporary-value range to avoid collisions.
    private static int _clearedKeySeed = -1_000_000_000;

    private static readonly MethodInfo ConfigureTenantFilterMethod = typeof(AppDbContext)
        .GetMethod(nameof(ConfigureTenantScope), BindingFlags.Instance | BindingFlags.NonPublic)!;

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Store enums as strings for readability in the database.
        b.Entity<Membership>().Property(x => x.Role).HasConversion<string>();
        b.Entity<Invitation>().Property(x => x.Role).HasConversion<string>();
        b.Entity<Performer>().Property(x => x.Gender).HasConversion<string>();
        b.Entity<Conflict>().Property(x => x.Type).HasConversion<string>();
        b.Entity<Conflict>().Property(x => x.Weekday).HasConversion<string>();
        b.Entity<Rehearsal>().Property(x => x.Type).HasConversion<string>();
        b.Entity<RehearsalAttendance>().Property(x => x.Status).HasConversion<string>();
        b.Entity<ProductionStaff>().Property(x => x.Role).HasConversion<string>();
        b.Entity<MusicalNumber>().Property(x => x.TeachStatus).HasConversion<string>();
        b.Entity<CostumePiece>().Property(x => x.Gender).HasConversion<string>();
        b.Entity<CostumePiece>().Property(x => x.Status).HasConversion<string>();
        b.Entity<Prop>().Property(x => x.Status).HasConversion<string>();

        // Composite keys (join rows without a store-generated Id).
        b.Entity<NumberCast>().HasKey(x => new { x.MusicalNumberId, x.PerformerId });
        b.Entity<ProductionAccess>().HasKey(x => new { x.MembershipId, x.ProductionId });
        b.Entity<PerformerGuardian>().HasKey(x => new { x.PerformerId, x.GuardianId });
        b.Entity<RehearsalAttendee>().HasKey(x => new { x.RehearsalId, x.PerformerId });
        b.Entity<RehearsalAttendance>().HasKey(x => new { x.RehearsalId, x.PerformerId });
        b.Entity<ProductionStaff>().HasKey(x => new { x.ProductionId, x.StaffMemberId, x.Role });
        b.Entity<SceneCharacter>().HasKey(x => new { x.SceneId, x.RoleId });
        b.Entity<NumberCharacter>().HasKey(x => new { x.MusicalNumberId, x.RoleId });

        // A Clerk user can belong to several companies, but only once each.
        b.Entity<Membership>().HasIndex(x => new { x.ClerkUserId, x.TenantId }).IsUnique();
        b.Entity<Membership>().HasIndex(x => x.ClerkUserId); // fast lookup of a user's companies
        // Join codes are redeemed by exact lookup and must be unique.
        b.Entity<Invitation>().HasIndex(x => x.Code).IsUnique();

        // A performer is in a production at most once.
        b.Entity<CastMembership>().HasIndex(x => new { x.ProductionId, x.PerformerId }).IsUnique();

        // Deleting a cast group must NOT drop performers from the show — their
        // membership falls back to "ungrouped" (CastGroupId null).
        b.Entity<CastMembership>()
            .HasOne(x => x.CastGroup).WithMany().HasForeignKey(x => x.CastGroupId)
            .OnDelete(DeleteBehavior.SetNull);
        // Deleting a performer removes their production memberships and number casts.
        b.Entity<CastMembership>()
            .HasOne(x => x.Performer).WithMany().HasForeignKey(x => x.PerformerId)
            .OnDelete(DeleteBehavior.Cascade);
        // Deleting a performer leaves their roles behind as "uncast" characters.
        b.Entity<Role>()
            .HasOne(x => x.Performer).WithMany().HasForeignKey(x => x.PerformerId)
            .OnDelete(DeleteBehavior.SetNull);

        // Access grants die with the member or the show.
        b.Entity<ProductionAccess>()
            .HasOne(x => x.Membership).WithMany().HasForeignKey(x => x.MembershipId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<ProductionAccess>()
            .HasOne(x => x.Production).WithMany().HasForeignKey(x => x.ProductionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Conflicts die with the show or the performer.
        b.Entity<Conflict>()
            .HasOne(x => x.Production).WithMany().HasForeignKey(x => x.ProductionId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<Conflict>()
            .HasOne(x => x.Performer).WithMany().HasForeignKey(x => x.PerformerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Rehearsal slots die with the show; deleting a number leaves its slots as
        // "general" sessions rather than losing the schedule history.
        b.Entity<Rehearsal>()
            .HasOne(x => x.Production).WithMany().HasForeignKey(x => x.ProductionId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<Rehearsal>()
            .HasOne(x => x.MusicalNumber).WithMany().HasForeignKey(x => x.MusicalNumberId)
            .OnDelete(DeleteBehavior.SetNull);
        b.Entity<RehearsalAttendee>()
            .HasOne(x => x.Rehearsal).WithMany().HasForeignKey(x => x.RehearsalId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<RehearsalAttendee>()
            .HasOne(x => x.Performer).WithMany().HasForeignKey(x => x.PerformerId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<RehearsalAttendance>()
            .HasOne(x => x.Rehearsal).WithMany().HasForeignKey(x => x.RehearsalId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<RehearsalAttendance>()
            .HasOne(x => x.Performer).WithMany().HasForeignKey(x => x.PerformerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deleting an act drops its numbers to "Unassigned", never deletes them.
        b.Entity<MusicalNumber>()
            .HasOne(x => x.Act).WithMany().HasForeignKey(x => x.ActId)
            .OnDelete(DeleteBehavior.SetNull);
        // Deleting a staff member clears the choreographer, keeps the number.
        b.Entity<MusicalNumber>()
            .HasOne(x => x.Choreographer).WithMany().HasForeignKey(x => x.ChoreographerStaffId)
            .OnDelete(DeleteBehavior.SetNull);
        // Deleting a scene un-nests its numbers (keeps them and their act).
        b.Entity<MusicalNumber>()
            .HasOne(x => x.Scene).WithMany().HasForeignKey(x => x.SceneId)
            .OnDelete(DeleteBehavior.SetNull);

        // Creative-team assignments die with the show or the staff member.
        b.Entity<ProductionStaff>()
            .HasOne(x => x.Production).WithMany().HasForeignKey(x => x.ProductionId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<ProductionStaff>()
            .HasOne(x => x.StaffMember).WithMany().HasForeignKey(x => x.StaffMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        // Costumes die with their number; assignments also with the performer.
        b.Entity<CostumePiece>()
            .HasOne(x => x.MusicalNumber).WithMany().HasForeignKey(x => x.MusicalNumberId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<CostumeAssignment>()
            .HasOne(x => x.MusicalNumber).WithMany().HasForeignKey(x => x.MusicalNumberId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<CostumeAssignment>()
            .HasOne(x => x.Performer).WithMany().HasForeignKey(x => x.PerformerId)
            .OnDelete(DeleteBehavior.Cascade);
        // Deleting a look (piece) leaves its wearers assigned but "unassigned look".
        b.Entity<CostumeAssignment>()
            .HasOne(x => x.CostumePiece).WithMany().HasForeignKey(x => x.CostumePieceId)
            .OnDelete(DeleteBehavior.SetNull);
        b.Entity<CostumeAssignment>().HasIndex(x => new { x.MusicalNumberId, x.PerformerId }).IsUnique();

        // Formations die with their number; coordinates stored as jsonb.
        b.Entity<Formation>()
            .HasOne(x => x.MusicalNumber).WithMany().HasForeignKey(x => x.MusicalNumberId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<Formation>().Property(x => x.Coordinates).HasColumnType("jsonb");

        // Deleting an act drops its scenes to "Unassigned", never deletes them.
        b.Entity<Scene>()
            .HasOne(x => x.Act).WithMany().HasForeignKey(x => x.ActId)
            .OnDelete(DeleteBehavior.SetNull);
        // Character presence rows die with their scene/number or the character.
        b.Entity<SceneCharacter>()
            .HasOne(x => x.Scene).WithMany().HasForeignKey(x => x.SceneId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<SceneCharacter>()
            .HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<NumberCharacter>()
            .HasOne(x => x.MusicalNumber).WithMany().HasForeignKey(x => x.MusicalNumberId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<NumberCharacter>()
            .HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Props: assignments die with their prop or their scene.
        b.Entity<PropAssignment>()
            .HasOne(x => x.Prop).WithMany().HasForeignKey(x => x.PropId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<PropAssignment>()
            .HasOne(x => x.Scene).WithMany().HasForeignKey(x => x.SceneId)
            .OnDelete(DeleteBehavior.Cascade);

        // Helpful lookup indexes.
        b.Entity<Conflict>().HasIndex(x => new { x.ProductionId, x.PerformerId });
        b.Entity<Rehearsal>().HasIndex(x => new { x.ProductionId, x.Date });
        b.Entity<Season>().HasIndex(x => new { x.TenantId, x.Year });
        b.Entity<MusicalNumber>().HasIndex(x => new { x.ProductionId, x.OrderIndex });
        b.Entity<Scene>().HasIndex(x => new { x.ProductionId, x.OrderIndex });
        b.Entity<Prop>().HasIndex(x => new { x.ProductionId, x.OrderIndex });

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
        // Guards a Postgres-specific hazard (IDENTITY BY DEFAULT honoring client
        // values). The in-memory demo provider assigns REAL ids at Add time
        // (never temporary), which this loop would wrongly treat as client input.
        if (Database.IsInMemory()) return;

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
                    var property = entry.Property(prop.Name);
                    // EF's own placeholder (entity added with default id) — the
                    // store already assigns; leave it alone.
                    if (property.IsTemporary) continue;
                    // Client-supplied value: replace with a UNIQUE placeholder
                    // (a shared constant like 0 would collide in the identity map
                    // when several rows of one type are saved together), then mark
                    // temporary so the database generates the real key.
                    var placeholder = Interlocked.Decrement(ref _clearedKeySeed);
                    // Box exactly the key's CLR type (a long boxed into an int key throws).
                    property.CurrentValue = prop.ClrType == typeof(long)
                        ? (object)(long)placeholder
                        : (object)placeholder;
                    property.IsTemporary = true;
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
