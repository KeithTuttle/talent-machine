using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>Per-show performer scheduling conflicts (see Conflict model).</summary>
[ApiController]
[Route("api/[controller]")]
public class ConflictsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public ConflictsController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public record BulkRequest(int ProductionId, List<Conflict> Conflicts);

    // GET /api/conflicts?productionId=&performerId=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Conflict>>> GetAll(
        [FromQuery] int? productionId, [FromQuery] int? performerId)
    {
        var query = _db.Conflicts.AsQueryable();
        var accessible = _tenant.AccessibleProductionIds;
        if (accessible is not null)
            query = query.Where(c => accessible.Contains(c.ProductionId));
        if (productionId is not null)
            query = query.Where(c => c.ProductionId == productionId);
        if (performerId is not null)
            query = query.Where(c => c.PerformerId == performerId);
        return await query.OrderBy(c => c.StartDate).ThenBy(c => c.Id).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Conflict>> Get(int id)
    {
        var conflict = await _db.FindScopedAsync<Conflict>(id);
        if (conflict is null || !_tenant.CanAccessProduction(conflict.ProductionId)) return NotFound();
        return conflict;
    }

    [HttpPost]
    public async Task<ActionResult<Conflict>> Create(Conflict conflict)
    {
        if (!_tenant.CanAccessProduction(conflict.ProductionId)) return StatusCode(403);
        _db.Conflicts.Add(conflict);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = conflict.Id }, conflict);
    }

    // POST /api/conflicts/bulk — CSV import lands here: one access check, many rows.
    [HttpPost("bulk")]
    public async Task<ActionResult<IEnumerable<Conflict>>> CreateBulk(BulkRequest input)
    {
        if (!_tenant.CanAccessProduction(input.ProductionId)) return StatusCode(403);

        // Only performers actually in this production's cast can carry conflicts.
        var castPerformerIds = await _db.CastMemberships
            .Where(m => m.ProductionId == input.ProductionId)
            .Select(m => m.PerformerId)
            .ToListAsync();

        var rows = input.Conflicts
            .Where(c => castPerformerIds.Contains(c.PerformerId))
            .ToList();
        foreach (var c in rows)
        {
            c.Id = 0;
            c.ProductionId = input.ProductionId;
        }
        _db.Conflicts.AddRange(rows);
        await _db.SaveChangesAsync();
        return Ok(rows);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Conflict input)
    {
        if (id != input.Id) return BadRequest();
        if (!_tenant.CanAccessProduction(input.ProductionId)) return NotFound();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var conflict = await _db.FindScopedAsync<Conflict>(id);
        if (conflict is null || !_tenant.CanAccessProduction(conflict.ProductionId)) return NotFound();
        _db.Conflicts.Remove(conflict);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
