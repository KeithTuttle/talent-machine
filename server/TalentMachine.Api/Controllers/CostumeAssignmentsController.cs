using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>Per-performer costume detail for a number (which costume, size, notes, fitting).</summary>
[ApiController]
[Route("api/[controller]")]
public class CostumeAssignmentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public CostumeAssignmentsController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    // GET /api/costumeassignments?numberId=|?productionId=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CostumeAssignment>>> GetAll(
        [FromQuery] int? numberId, [FromQuery] int? productionId)
    {
        var query = _db.CostumeAssignments.AsQueryable();
        var accessible = _tenant.AccessibleProductionIds;
        if (accessible is not null)
            query = query.Where(a => a.MusicalNumber != null && accessible.Contains(a.MusicalNumber.ProductionId));
        if (numberId is not null)
            query = query.Where(a => a.MusicalNumberId == numberId);
        if (productionId is not null)
            query = query.Where(a => a.MusicalNumber != null && a.MusicalNumber.ProductionId == productionId);
        return await query.ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<CostumeAssignment>> Create(CostumeAssignment input)
    {
        if (!await CanAccessNumber(input.MusicalNumberId)) return StatusCode(403);
        var existing = await _db.CostumeAssignments.FirstOrDefaultAsync(
            a => a.MusicalNumberId == input.MusicalNumberId && a.PerformerId == input.PerformerId);
        if (existing is not null)
        {
            existing.CostumeId = input.CostumeId;
            existing.Size = input.Size;
            existing.Notes = input.Notes;
            existing.IsFitted = input.IsFitted;
            await _db.SaveChangesAsync();
            return Ok(existing);
        }
        _db.CostumeAssignments.Add(input);
        await _db.SaveChangesAsync();
        return Ok(input);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CostumeAssignment input)
    {
        if (id != input.Id) return BadRequest();
        if (!await CanAccessNumber(input.MusicalNumberId)) return NotFound();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    // DELETE /api/costumeassignments?numberId=&performerId=
    [HttpDelete]
    public async Task<IActionResult> Remove([FromQuery] int numberId, [FromQuery] int performerId)
    {
        var row = await _db.CostumeAssignments.FirstOrDefaultAsync(
            a => a.MusicalNumberId == numberId && a.PerformerId == performerId);
        if (row is null || !await CanAccessNumber(row.MusicalNumberId)) return NotFound();
        _db.CostumeAssignments.Remove(row);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<bool> CanAccessNumber(int numberId)
    {
        var pid = await _db.Numbers.Where(n => n.Id == numberId).Select(n => (int?)n.ProductionId).FirstOrDefaultAsync();
        return pid is not null && _tenant.CanAccessProduction(pid.Value);
    }
}
