using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>Per-rehearsal attendance records (Present / Absent / Excused). Upsert POST.</summary>
[ApiController]
[Route("api/[controller]")]
public class RehearsalAttendanceController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public RehearsalAttendanceController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public record MarkRequest(int RehearsalId, int PerformerId, AttendanceStatus Status);

    // GET /api/rehearsalattendance?rehearsalId=|?productionId=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RehearsalAttendance>>> GetAll(
        [FromQuery] int? rehearsalId, [FromQuery] int? productionId)
    {
        var query = _db.RehearsalAttendances.AsQueryable();
        var accessible = _tenant.AccessibleProductionIds;
        if (accessible is not null)
            query = query.Where(a => a.Rehearsal != null && accessible.Contains(a.Rehearsal.ProductionId));
        if (rehearsalId is not null)
            query = query.Where(a => a.RehearsalId == rehearsalId);
        if (productionId is not null)
            query = query.Where(a => a.Rehearsal != null && a.Rehearsal.ProductionId == productionId);
        return await query.ToListAsync();
    }

    // POST /api/rehearsalattendance — upsert one kid's status for one rehearsal.
    [HttpPost]
    public async Task<ActionResult<RehearsalAttendance>> Mark(MarkRequest input)
    {
        var rehearsal = await _db.Rehearsals.FirstOrDefaultAsync(r => r.Id == input.RehearsalId);
        if (rehearsal is null || !_tenant.CanAccessProduction(rehearsal.ProductionId)) return NotFound();

        var existing = await _db.RehearsalAttendances.FirstOrDefaultAsync(
            a => a.RehearsalId == input.RehearsalId && a.PerformerId == input.PerformerId);
        if (existing is not null)
        {
            existing.Status = input.Status;
            await _db.SaveChangesAsync();
            return Ok(existing);
        }

        var row = new RehearsalAttendance
        {
            RehearsalId = input.RehearsalId,
            PerformerId = input.PerformerId,
            Status = input.Status,
        };
        _db.RehearsalAttendances.Add(row);
        await _db.SaveChangesAsync();
        return Ok(row);
    }

    // DELETE /api/rehearsalattendance?rehearsalId=&performerId= — unset (back to unrecorded).
    [HttpDelete]
    public async Task<IActionResult> Unset([FromQuery] int rehearsalId, [FromQuery] int performerId)
    {
        var row = await _db.RehearsalAttendances
            .Include(a => a.Rehearsal)
            .FirstOrDefaultAsync(a => a.RehearsalId == rehearsalId && a.PerformerId == performerId);
        if (row is null || row.Rehearsal is null
            || !_tenant.CanAccessProduction(row.Rehearsal.ProductionId)) return NotFound();

        _db.RehearsalAttendances.Remove(row);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
