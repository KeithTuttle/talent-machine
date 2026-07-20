using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Data;

/// <summary>
/// Seeds a realistic demo production into the database on startup — registered
/// only when Database:UseInMemory is true (see Program.cs), i.e. demo mode with
/// no Postgres. Rows carry TenantId 0, which is exactly what the auth-off query
/// filter matches, so the whole app lights up without any setup. Dates are
/// relative to "today" so conflicts / rehearsals / attendance always look alive.
/// Fully defensive: any exception is a logged no-op.
/// </summary>
public class DemoSeeder : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DemoSeeder> _logger;

    public DemoSeeder(IServiceProvider services, ILogger<DemoSeeder> logger)
    {
        _services = services;
        _logger = logger;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (await db.Seasons.IgnoreQueryFilters().AnyAsync(cancellationToken)) return;
            await SeedAsync(db);
            _logger.LogInformation("Demo data seeded (in-memory database).");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Demo seeding failed — starting empty.");
        }
    }

    private static async Task SeedAsync(AppDbContext db)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        // Anchor recurring demo dates to real weekdays around today.
        DateOnly Next(DayOfWeek day, int weeks = 0)
        {
            var d = today.AddDays(((int)day - (int)today.DayOfWeek + 7) % 7);
            return d.AddDays(7 * weeks);
        }
        var lastSaturday = Next(DayOfWeek.Saturday).AddDays(-7);
        var thisSaturday = Next(DayOfWeek.Saturday);
        var thisSunday = Next(DayOfWeek.Sunday);

        // --- Seasons & productions ------------------------------------------
        var s2025 = new Season { Year = today.Year - 1, Name = $"Summer {today.Year - 1}" };
        var s2026 = new Season { Year = today.Year, Name = $"Summer {today.Year}" };
        db.Seasons.AddRange(s2025, s2026);
        await db.SaveChangesAsync();

        var seussical = new Production
        {
            SeasonId = s2025.Id,
            Title = "Seussical JR.",
            OpeningDate = new DateOnly(today.Year - 1, 7, 26),
        };
        var annie = new Production
        {
            SeasonId = s2026.Id,
            Title = "Annie JR.",
            OpeningDate = today.AddDays(35),
            Notes = "Two performances; tech week starts two weeks out.",
        };
        db.Productions.AddRange(seussical, annie);
        await db.SaveChangesAsync();

        // --- Performers + guardians -----------------------------------------
        Performer P(string first, string last, int age, Gender g, string? notes = null) => new()
        {
            FirstName = first,
            LastName = last,
            Gender = g,
            DateOfBirth = today.AddYears(-age).AddDays(-(first.Length * 17 % 300)),
            Notes = notes,
        };

        var kids = new List<Performer>
        {
            P("Olivia", "Smith", 14, Gender.Female, "Strong belt; peanut allergy."),
            P("Liam", "Smith", 11, Gender.Male),
            P("Emma", "Johnson", 16, Gender.Female),
            P("Noah", "Williams", 12, Gender.Male),
            P("Ava", "Brown", 9, Gender.Female),
            P("Sophia", "Jones", 13, Gender.Female, "Dance captain material."),
            P("Mason", "Garcia", 15, Gender.Male),
            P("Isabella", "Miller", 10, Gender.Female),
            P("Ethan", "Davis", 17, Gender.Male),
            P("Mia", "Rodriguez", 8, Gender.Female),
            P("Lucas", "Martinez", 12, Gender.Male),
            P("Charlotte", "Hernandez", 14, Gender.NonBinary),
            P("Henry", "Lopez", 10, Gender.Male),
            P("Amelia", "Gonzalez", 13, Gender.Female),
        };
        db.Performers.AddRange(kids);
        await db.SaveChangesAsync();

        var guardians = new List<Guardian>
        {
            new() { Name = "Karen Smith", Email = "karen.smith@example.com", Phone = "410-555-0101" },
            new() { Name = "David Smith", Email = "david.smith@example.com", Phone = "410-555-0102" },
            new() { Name = "Patricia Johnson", Email = "pjohnson@example.com", Phone = "410-555-0110" },
            new() { Name = "Robert Williams", Email = "rwilliams@example.com" },
            new() { Name = "Linda Brown", Email = "lbrown@example.com", Phone = "410-555-0130" },
            new() { Name = "Maria Garcia", Email = "mgarcia@example.com" },
            new() { Name = "James Davis", Email = "jdavis@example.com", Phone = "410-555-0150" },
            new() { Name = "Sofia Rodriguez", Email = "srodriguez@example.com" },
        };
        db.Guardians.AddRange(guardians);
        await db.SaveChangesAsync();

        // Siblings Olivia + Liam share both Smith parents.
        db.PerformerGuardians.AddRange(
            new PerformerGuardian { PerformerId = kids[0].Id, GuardianId = guardians[0].Id },
            new PerformerGuardian { PerformerId = kids[0].Id, GuardianId = guardians[1].Id },
            new PerformerGuardian { PerformerId = kids[1].Id, GuardianId = guardians[0].Id },
            new PerformerGuardian { PerformerId = kids[1].Id, GuardianId = guardians[1].Id },
            new PerformerGuardian { PerformerId = kids[2].Id, GuardianId = guardians[2].Id },
            new PerformerGuardian { PerformerId = kids[3].Id, GuardianId = guardians[3].Id },
            new PerformerGuardian { PerformerId = kids[4].Id, GuardianId = guardians[4].Id },
            new PerformerGuardian { PerformerId = kids[6].Id, GuardianId = guardians[5].Id },
            new PerformerGuardian { PerformerId = kids[8].Id, GuardianId = guardians[6].Id },
            new PerformerGuardian { PerformerId = kids[9].Id, GuardianId = guardians[7].Id });
        await db.SaveChangesAsync();

        // --- Annie: groups, levels, roles -----------------------------------
        var gold = new CastGroup { ProductionId = annie.Id, Name = "Gold Group", Color = "#EAB308", OrderIndex = 1 };
        var blue = new CastGroup { ProductionId = annie.Id, Name = "Blue Group", Color = "#3B82F6", OrderIndex = 2 };
        db.CastGroups.AddRange(gold, blue);
        await db.SaveChangesAsync();

        // Everyone in Annie; first 6 kids Gold, rest Blue; one show note.
        var memberships = kids.Select((k, i) => new CastMembership
        {
            ProductionId = annie.Id,
            PerformerId = k.Id,
            CastGroupId = i < 6 ? gold.Id : blue.Id,
            Notes = i == 0 ? "Understudy: Emma. Mic 4." : null,
        }).ToList();
        db.CastMemberships.AddRange(memberships);

        db.Roles.AddRange(
            new Role { ProductionId = annie.Id, Name = "Annie", PerformerId = kids[0].Id, OrderIndex = 1 },
            new Role { ProductionId = annie.Id, Name = "Miss Hannigan", PerformerId = kids[2].Id, OrderIndex = 2 },
            new Role { ProductionId = annie.Id, Name = "Daddy Warbucks", PerformerId = kids[8].Id, OrderIndex = 3 },
            new Role { ProductionId = annie.Id, Name = "Grace Farrell", PerformerId = kids[5].Id, OrderIndex = 4 },
            new Role { ProductionId = annie.Id, Name = "Rooster", OrderIndex = 5 });
        await db.SaveChangesAsync();

        // --- Staff directory + creative team --------------------------------
        var staff = new List<StaffMember>
        {
            new() { Name = "Rebecca Hall", Email = "rebecca.hall@example.com", Phone = "410-555-0200" },
            new() { Name = "Marcus Lee", Email = "marcus.lee@example.com", Phone = "410-555-0201" },
            new() { Name = "Priya Anand", Email = "priya.anand@example.com" },
            new() { Name = "Tom Becker", Email = "tom.becker@example.com", Phone = "410-555-0203" },
        };
        db.StaffMembers.AddRange(staff);
        await db.SaveChangesAsync();

        db.ProductionStaff.AddRange(
            new ProductionStaff { ProductionId = annie.Id, StaffMemberId = staff[0].Id, Role = StaffRole.Director },
            new ProductionStaff { ProductionId = annie.Id, StaffMemberId = staff[1].Id, Role = StaffRole.Choreographer },
            new ProductionStaff { ProductionId = annie.Id, StaffMemberId = staff[2].Id, Role = StaffRole.MusicDirector },
            new ProductionStaff { ProductionId = annie.Id, StaffMemberId = staff[3].Id, Role = StaffRole.Producer },
            // Rebecca also directed last year's show (returning-people history).
            new ProductionStaff { ProductionId = seussical.Id, StaffMemberId = staff[0].Id, Role = StaffRole.Director });
        await db.SaveChangesAsync();

        // --- Acts + numbers + casting ---------------------------------------
        var act1 = new Act { ProductionId = annie.Id, Name = "Act 1", OrderIndex = 1 };
        var act2 = new Act { ProductionId = annie.Id, Name = "Act 2", OrderIndex = 2 };
        db.Acts.AddRange(act1, act2);
        await db.SaveChangesAsync();

        MusicalNumber N(string title, int order, Act? act, string songwriter = "Strouse / Charnin") => new()
        {
            ProductionId = annie.Id,
            Title = title,
            Songwriter = songwriter,
            ActId = act?.Id,
            OrderIndex = order,
        };
        var maybe = N("Maybe", 1, act1);
        var hardKnock = N("It's the Hard-Knock Life", 2, act1);
        var tomorrow = N("Tomorrow", 3, act1);
        var littleGirls = N("Little Girls", 4, act1);
        var nyc = N("N.Y.C.", 1, act2);
        var easyStreet = N("Easy Street", 2, act2);
        var reprise = N("Tomorrow (Reprise)", 3, act2);
        var finale = N("Finale: A New Deal for Christmas", 4, act2);
        var uncast = N("I Don't Need Anything But You", 1, null);
        db.Numbers.AddRange(maybe, hardKnock, tomorrow, littleGirls, nyc, easyStreet, reprise, finale, uncast);
        await db.SaveChangesAsync();

        void Cast(MusicalNumber n, params Performer[] who) =>
            db.NumberCasts.AddRange(who.Select(k => new NumberCast { MusicalNumberId = n.Id, PerformerId = k.Id }));

        var orphans = new[] { kids[0], kids[4], kids[7], kids[9], kids[11], kids[13] };
        Cast(maybe, kids[0]);
        Cast(hardKnock, orphans);
        Cast(tomorrow, kids[0], kids[8]);
        Cast(littleGirls, kids[2]);
        Cast(nyc, kids.ToArray()); // full company
        Cast(easyStreet, kids[2], kids[6], kids[13]);
        Cast(reprise, kids[0], kids[5], kids[8]);
        Cast(finale, kids.ToArray());
        await db.SaveChangesAsync();

        // --- Conflicts -------------------------------------------------------
        db.Conflicts.AddRange(
            new Conflict
            {
                ProductionId = annie.Id, PerformerId = kids[3].Id, Type = ConflictType.OneOff,
                StartDate = thisSaturday, EndDate = thisSunday, Reason = "Family wedding",
            },
            new Conflict
            {
                ProductionId = annie.Id, PerformerId = kids[6].Id, Type = ConflictType.Weekly,
                StartDate = today.AddDays(-60), Weekday = DayOfWeek.Saturday, Reason = "Travel soccer (mornings)",
            },
            new Conflict
            {
                ProductionId = annie.Id, PerformerId = kids[5].Id, Type = ConflictType.Weekly,
                StartDate = today.AddDays(-90), EndDate = today.AddDays(40),
                Weekday = DayOfWeek.Tuesday, Reason = "Dance class",
            },
            new Conflict
            {
                ProductionId = annie.Id, PerformerId = kids[9].Id, Type = ConflictType.OneOff,
                StartDate = today.AddDays(12), EndDate = today.AddDays(18), Reason = "Family vacation",
            });
        await db.SaveChangesAsync();

        // --- Rehearsals: last weekend (with attendance) + this weekend ------
        Rehearsal R(DateOnly date, int fromHour, int toHour, RehearsalType type, MusicalNumber? n, string? notes = null) => new()
        {
            ProductionId = annie.Id,
            Date = date,
            StartTime = new TimeOnly(fromHour, 0),
            EndTime = new TimeOnly(toHour, 0),
            Type = type,
            MusicalNumberId = n?.Id,
            Notes = notes,
        };

        var pastMusic = R(lastSaturday, 9, 10, RehearsalType.Music, hardKnock);
        var pastDance = R(lastSaturday, 10, 12, RehearsalType.Dance, hardKnock, "Buckets + brooms choreography");
        var pastRun = R(lastSaturday, 13, 15, RehearsalType.Runthrough, null, "Act 1 stumble-through");
        var nextMusic = R(thisSaturday, 9, 10, RehearsalType.Music, nyc);
        var nextDance = R(thisSaturday, 10, 12, RehearsalType.Dance, nyc, "Full company — spacing on the big platform");
        var nextBlock = R(thisSunday, 13, 15, RehearsalType.Blocking, easyStreet);
        db.Rehearsals.AddRange(pastMusic, pastDance, pastRun, nextMusic, nextDance, nextBlock);
        await db.SaveChangesAsync();

        // Act-1 run-through: leads only via explicit adds (number-less slot).
        db.RehearsalAttendees.AddRange(
            new RehearsalAttendee { RehearsalId = pastRun.Id, PerformerId = kids[0].Id },
            new RehearsalAttendee { RehearsalId = pastRun.Id, PerformerId = kids[2].Id },
            new RehearsalAttendee { RehearsalId = pastRun.Id, PerformerId = kids[8].Id },
            // Mason excluded from last Saturday's dance block (soccer).
            new RehearsalAttendee { RehearsalId = pastDance.Id, PerformerId = kids[6].Id, IsExcluded = true });

        // Attendance for last Saturday's music rehearsal: mostly present, one
        // excused (weekly soccer conflict), one UNexcused absence.
        foreach (var k in orphans)
        {
            var status = k == kids[11] ? AttendanceStatus.Absent : AttendanceStatus.Present;
            db.RehearsalAttendances.Add(new RehearsalAttendance
            {
                RehearsalId = pastMusic.Id, PerformerId = k.Id, Status = status,
            });
        }
        db.RehearsalAttendances.AddRange(
            new RehearsalAttendance { RehearsalId = pastRun.Id, PerformerId = kids[0].Id, Status = AttendanceStatus.Present },
            new RehearsalAttendance { RehearsalId = pastRun.Id, PerformerId = kids[2].Id, Status = AttendanceStatus.Present },
            new RehearsalAttendance { RehearsalId = pastRun.Id, PerformerId = kids[8].Id, Status = AttendanceStatus.Excused });
        await db.SaveChangesAsync();

        // --- A little multi-year history: Seussical with overlapping kids ---
        var sGroup = new CastGroup { ProductionId = seussical.Id, Name = "Whos", Color = "#F97316", OrderIndex = 1 };
        db.CastGroups.Add(sGroup);
        await db.SaveChangesAsync();
        foreach (var k in new[] { kids[0], kids[2], kids[5], kids[8], kids[13] })
            db.CastMemberships.Add(new CastMembership { ProductionId = seussical.Id, PerformerId = k.Id, CastGroupId = sGroup.Id });
        var horton = new MusicalNumber { ProductionId = seussical.Id, Title = "Horton Hears a Who", Songwriter = "Flaherty / Ahrens", OrderIndex = 1 };
        db.Numbers.Add(horton);
        await db.SaveChangesAsync();
        db.NumberCasts.AddRange(
            new NumberCast { MusicalNumberId = horton.Id, PerformerId = kids[0].Id },
            new NumberCast { MusicalNumberId = horton.Id, PerformerId = kids[8].Id });
        await db.SaveChangesAsync();
    }
}
