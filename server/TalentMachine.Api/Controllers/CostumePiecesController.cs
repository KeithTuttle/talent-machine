using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>Per-number costume specs (description, accessories, shoes, links).</summary>
[ApiController]
[Route("api/[controller]")]
public class CostumePiecesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public CostumePiecesController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    // GET /api/costumepieces?numberId=|?productionId=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CostumePiece>>> GetAll(
        [FromQuery] int? numberId, [FromQuery] int? productionId)
    {
        var query = _db.CostumePieces.AsQueryable();
        var accessible = _tenant.AccessibleProductionIds;
        if (accessible is not null)
            query = query.Where(p => p.MusicalNumber != null && accessible.Contains(p.MusicalNumber.ProductionId));
        if (numberId is not null)
            query = query.Where(p => p.MusicalNumberId == numberId);
        if (productionId is not null)
            query = query.Where(p => p.MusicalNumber != null && p.MusicalNumber.ProductionId == productionId);
        return await query.ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<CostumePiece>> Create(CostumePiece piece)
    {
        if (!await CanAccessNumber(piece.MusicalNumberId)) return StatusCode(403);
        _db.CostumePieces.Add(piece);
        await _db.SaveChangesAsync();
        return Ok(piece);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CostumePiece input)
    {
        if (id != input.Id) return BadRequest();
        if (!await CanAccessNumber(input.MusicalNumberId)) return NotFound();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var piece = await _db.FindScopedAsync<CostumePiece>(id);
        if (piece is null || !await CanAccessNumber(piece.MusicalNumberId)) return NotFound();
        _db.CostumePieces.Remove(piece);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<bool> CanAccessNumber(int numberId)
    {
        var pid = await _db.Numbers.Where(n => n.Id == numberId).Select(n => (int?)n.ProductionId).FirstOrDefaultAsync();
        return pid is not null && _tenant.CanAccessProduction(pid.Value);
    }
}
