using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>
/// Read-only production home: one aggregate for the dashboard so the client
/// renders a thin view instead of stitching several endpoints together.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public DashboardController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public record CountdownDto(string? OpeningDate, int? DaysToOpen);
    public record RollupsDto(
        int NumbersTotal, int TeachComplete, int TeachTaught, int TeachNeedsReview, int NotTaught,
        int NumbersWithCast, int RolesTotal, int RolesCast, int PerformersInShow);
    public record WeekSlotDto(
        int Id, string Date, string StartTime, string EndTime, string Type,
        string? NumberTitle, int Attendees, int Conflicts);
    public record AtRiskDto(int PerformerId, string Name, int Present, int Total, int Percent);
    public record AttendanceDto(int RecordedSessions, int AvgPercent, List<AtRiskDto> AtRisk);
    public record CostumesDto(
        int Total, int Ready, int Sourced, int Needed,
        int FittingsDone, int FittingsTotal, int QuickChanges);
    public record DashboardDto(
        int ProductionId, string Title,
        CountdownDto Countdown, RollupsDto Rollups,
        List<WeekSlotDto> ThisWeek, AttendanceDto Attendance, CostumesDto Costumes);

    // GET /api/dashboard?productionId=
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get([FromQuery] int productionId)
    {
        var production = await _db.Productions.FirstOrDefaultAsync(p => p.Id == productionId);
        if (production is null || !_tenant.CanAccessProduction(productionId)) return NotFound();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var weekEnd = today.AddDays(6);

        // Pull the production's slices once.
        var numbers = await _db.Numbers.Where(n => n.ProductionId == productionId).ToListAsync();
        var numberIds = numbers.Select(n => n.Id).ToHashSet();
        var casts = await _db.NumberCasts.Where(c => numberIds.Contains(c.MusicalNumberId)).ToListAsync();
        var roles = await _db.Roles.Where(r => r.ProductionId == productionId).ToListAsync();
        var memberships = await _db.CastMemberships.Where(m => m.ProductionId == productionId).ToListAsync();
        var rehearsals = await _db.Rehearsals.Where(r => r.ProductionId == productionId).ToListAsync();
        var rehearsalIds = rehearsals.Select(r => r.Id).ToHashSet();
        var attendees = await _db.RehearsalAttendees.Where(a => rehearsalIds.Contains(a.RehearsalId)).ToListAsync();
        var attendances = await _db.RehearsalAttendances.Where(a => rehearsalIds.Contains(a.RehearsalId)).ToListAsync();
        var conflicts = await _db.Conflicts.Where(c => c.ProductionId == productionId).ToListAsync();
        var performers = await _db.Performers.ToListAsync();
        var performerName = performers.ToDictionary(
            p => p.Id, p => $"{p.FirstName} {p.LastName}".Trim());

        // --- Countdown ---
        int? daysToOpen = production.OpeningDate is { } od ? od.DayNumber - today.DayNumber : null;
        var countdown = new CountdownDto(
            production.OpeningDate?.ToString("yyyy-MM-dd"), daysToOpen);

        // --- Rollups ---
        var numbersWithCast = casts.Select(c => c.MusicalNumberId).ToHashSet();
        var rollups = new RollupsDto(
            NumbersTotal: numbers.Count,
            TeachComplete: numbers.Count(n => n.TeachStatus == TeachStatus.Complete),
            TeachTaught: numbers.Count(n => n.TeachStatus == TeachStatus.Taught),
            TeachNeedsReview: numbers.Count(n => n.TeachStatus == TeachStatus.NeedsReview),
            NotTaught: numbers.Count(n => n.TeachStatus == null),
            NumbersWithCast: numbers.Count(n => numbersWithCast.Contains(n.Id)),
            RolesTotal: roles.Count,
            RolesCast: roles.Count(r => r.PerformerId != null),
            PerformersInShow: memberships.Count);

        // --- This week (rolling 7 days) ---
        bool ConflictActive(int performerId, DateOnly date) => conflicts.Any(c =>
            c.PerformerId == performerId &&
            (c.Type == ConflictType.OneOff
                ? c.StartDate <= date && date <= (c.EndDate ?? c.StartDate)
                : c.Weekday == date.DayOfWeek && c.StartDate <= date && date <= (c.EndDate ?? DateOnly.MaxValue)));

        var thisWeek = new List<WeekSlotDto>();
        foreach (var r in rehearsals
            .Where(r => r.Date >= today && r.Date <= weekEnd)
            .OrderBy(r => r.Date).ThenBy(r => r.StartTime))
        {
            // Resolved attendees = (number cast ∪ added) − excluded.
            var ids = new HashSet<int>(
                r.MusicalNumberId is { } nid ? casts.Where(c => c.MusicalNumberId == nid).Select(c => c.PerformerId) : []);
            foreach (var a in attendees.Where(a => a.RehearsalId == r.Id && !a.IsExcluded)) ids.Add(a.PerformerId);
            foreach (var a in attendees.Where(a => a.RehearsalId == r.Id && a.IsExcluded)) ids.Remove(a.PerformerId);

            thisWeek.Add(new WeekSlotDto(
                r.Id, r.Date.ToString("yyyy-MM-dd"),
                r.StartTime.ToString("HH:mm"), r.EndTime.ToString("HH:mm"),
                r.Type.ToString(),
                numbers.FirstOrDefault(n => n.Id == r.MusicalNumberId)?.Title,
                ids.Count,
                ids.Count(pid => ConflictActive(pid, r.Date))));
        }

        // --- Attendance flags ---
        // Rate = present / (present + unexcused-absent). Excused absences are a
        // recorded conflict, not a reliability concern, so they don't count against
        // the kid (and never trip the at-risk flag).
        var recordedRehearsals = attendances.Select(a => a.RehearsalId).ToHashSet();
        var byPerformer = attendances
            .GroupBy(a => a.PerformerId)
            .Select(g =>
            {
                var present = g.Count(a => a.Status == AttendanceStatus.Present);
                var accountable = g.Count(a => a.Status != AttendanceStatus.Excused);
                return new AtRiskDto(
                    g.Key,
                    performerName.GetValueOrDefault(g.Key, $"#{g.Key}"),
                    present, accountable, accountable == 0 ? 100 : (int)Math.Round(100.0 * present / accountable));
            })
            .ToList();
        var accountableRows = attendances.Count(a => a.Status != AttendanceStatus.Excused);
        var totalPresent = attendances.Count(a => a.Status == AttendanceStatus.Present);
        var attendance = new AttendanceDto(
            RecordedSessions: recordedRehearsals.Count,
            AvgPercent: accountableRows == 0 ? 100 : (int)Math.Round(100.0 * totalPresent / accountableRows),
            // Only flag kids who have at least one accountable session and fall below 75%.
            AtRisk: byPerformer.Where(p => p.Total > 0 && p.Percent < 75)
                .OrderBy(p => p.Percent).ThenBy(p => p.Name).ToList());

        // --- Costume readiness ---
        var catalog = await _db.Costumes.Where(c => c.ProductionId == productionId).ToListAsync();
        var costumeAssignments = await _db.CostumeAssignments
            .Where(a => numberIds.Contains(a.MusicalNumberId)).ToListAsync();
        var acts = await _db.Acts.Where(a => a.ProductionId == productionId).ToListAsync();
        var costumeNumbers = await _db.CostumeNumbers
            .Where(cn => numberIds.Contains(cn.MusicalNumberId)).ToListAsync();

        // Who needs a fitting: everyone the app can say is wearing a costume. That's
        // an explicit assignment OR — in a number with exactly one costume — simply
        // being in the number, since one costume dresses everyone (which is why the
        // per-kid picker stays hidden there). Counting only explicit assignments made
        // fittings in single-costume numbers invisible.
        var costumeById = catalog.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
        var assignmentByKey = costumeAssignments
            .GroupBy(a => (a.MusicalNumberId, a.PerformerId))
            .ToDictionary(g => g.Key, g => g.First());
        var wornByNumber = costumeNumbers.ToLookup(cn => cn.MusicalNumberId, cn => cn.CostumeId);

        var fittingsTotal = 0;
        var fittingsDone = 0;
        foreach (var n in numbers)
        {
            var people = casts.Where(c => c.MusicalNumberId == n.Id).Select(c => c.PerformerId)
                .Concat(costumeAssignments.Where(a => a.MusicalNumberId == n.Id).Select(a => a.PerformerId))
                .Distinct();
            foreach (var pid in people)
            {
                if (Services.CostumeChanges.CostumeIdFor(n, pid, assignmentByKey, costumeById, wornByNumber) is null)
                    continue;
                fittingsTotal++;
                if (assignmentByKey.TryGetValue((n.Id, pid), out var a) && a.IsFitted) fittingsDone++;
            }
        }

        var costumes = new CostumesDto(
            Total: catalog.Count,
            Ready: catalog.Count(c => c.Status == CostumeStatus.Ready),
            Sourced: catalog.Count(c => c.Status == CostumeStatus.Sourced),
            Needed: catalog.Count(c => c.Status == CostumeStatus.Needed),
            FittingsDone: fittingsDone,
            FittingsTotal: fittingsTotal,
            // Count MOMENTS (a spot in the show needing a dresser), not per-kid
            // changes, so this matches the overview panel's grouped rows.
            QuickChanges: Services.CostumeChanges
                .Detect(acts, numbers, casts, costumeAssignments, catalog, costumeNumbers)
                .Where(c => c.Buffer == 0)
                .Select(c => (c.From.Id, c.To.Id))
                .Distinct().Count());

        return new DashboardDto(
            production.Id, production.Title, countdown, rollups, thisWeek, attendance, costumes);
    }
}
