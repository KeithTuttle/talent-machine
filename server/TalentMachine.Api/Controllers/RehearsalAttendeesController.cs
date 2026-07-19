using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>Per-slot attendee overrides (add extra kid / exclude one). Upsert POST.</summary>
[ApiController]
[Route("api/[controller]")]
public class RehearsalAttendeesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public RehearsalAttendeesController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public record OverrideRequest(int RehearsalId, int PerformerId, bool IsExcluded);

    // GET /api/rehearsalattendees?rehearsalId=|?productionId=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RehearsalAttendee>>> GetAll(
        [FromQuery] int? rehearsalId, [FromQuery] int? productionId)
    {
        var query = _db.RehearsalAttendees.AsQueryable();
        var accessible = _tenant.AccessibleProductionIds;
        if (accessible is not null)
            query = query.Where(a => a.Rehearsal != null && accessible.Contains(a.Rehearsal.ProductionId));
        if (rehearsalId is not null)
            query = query.Where(a => a.RehearsalId == rehearsalId);
        if (productionId is not null)
            query = query.Where(a => a.Rehearsal != null && a.Rehearsal.ProductionId == productionId);
        return await query.ToListAsync();
    }

    // POST /api/rehearsalattendees — upsert an override row.
    [HttpPost]
    public async Task<ActionResult<RehearsalAttendee>> Upsert(OverrideRequest input)
    {
        var rehearsal = await _db.Rehearsals.FirstOrDefaultAsync(r => r.Id == input.RehearsalId);
        if (rehearsal is null || !_tenant.CanAccessProduction(rehearsal.ProductionId)) return NotFound();

        var existing = await _db.RehearsalAttendees.FirstOrDefaultAsync(
            a => a.RehearsalId == input.RehearsalId && a.PerformerId == input.PerformerId);
        if (existing is not null)
        {
            existing.IsExcluded = input.IsExcluded;
            await _db.SaveChangesAsync();
            return Ok(existing);
        }

        var row = new RehearsalAttendee
        {
            RehearsalId = input.RehearsalId,
            PerformerId = input.PerformerId,
            IsExcluded = input.IsExcluded,
        };
        _db.RehearsalAttendees.Add(row);
        await _db.SaveChangesAsync();
        return Ok(row);
    }

    // DELETE /api/rehearsalattendees?rehearsalId=&performerId= — drop the override
    // (back to "whatever the number's cast says").
    [HttpDelete]
    public async Task<IActionResult> Remove([FromQuery] int rehearsalId, [FromQuery] int performerId)
    {
        var row = await _db.RehearsalAttendees
            .Include(a => a.Rehearsal)
            .FirstOrDefaultAsync(a => a.RehearsalId == rehearsalId && a.PerformerId == performerId);
        if (row is null || row.Rehearsal is null
            || !_tenant.CanAccessProduction(row.Rehearsal.ProductionId)) return NotFound();

        _db.RehearsalAttendees.Remove(row);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
