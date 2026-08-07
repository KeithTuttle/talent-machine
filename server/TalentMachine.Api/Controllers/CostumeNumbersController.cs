using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>Which catalog costumes a number wears (join rows, no surrogate id).</summary>
[ApiController]
[Route("api/[controller]")]
public class CostumeNumbersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public CostumeNumbersController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public record LinkRequest(int CostumeId, int MusicalNumberId);

    // GET /api/costumenumbers?numberId=|?productionId=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CostumeNumber>>> GetAll(
        [FromQuery] int? numberId, [FromQuery] int? productionId)
    {
        var query = _db.CostumeNumbers.AsQueryable();
        var accessible = _tenant.AccessibleProductionIds;
        if (accessible is not null)
            query = query.Where(cn => cn.MusicalNumber != null && accessible.Contains(cn.MusicalNumber.ProductionId));
        if (numberId is not null)
            query = query.Where(cn => cn.MusicalNumberId == numberId);
        if (productionId is not null)
            query = query.Where(cn => cn.MusicalNumber != null && cn.MusicalNumber.ProductionId == productionId);
        return await query.ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<CostumeNumber>> Create(LinkRequest input)
    {
        if (!await CanAccessNumber(input.MusicalNumberId)) return StatusCode(403);

        // The costume must be one of THIS production's — FindScopedAsync goes
        // through the tenant filter, so another tenant's id simply isn't found.
        var costume = await _db.FindScopedAsync<Costume>(input.CostumeId);
        var productionId = await _db.Numbers.Where(n => n.Id == input.MusicalNumberId)
            .Select(n => (int?)n.ProductionId).FirstOrDefaultAsync();
        if (costume is null || costume.ProductionId != productionId) return NotFound();

        var existing = await _db.CostumeNumbers.FirstOrDefaultAsync(
            cn => cn.CostumeId == input.CostumeId && cn.MusicalNumberId == input.MusicalNumberId);
        if (existing is not null) return Ok(existing); // idempotent

        var row = new CostumeNumber { CostumeId = input.CostumeId, MusicalNumberId = input.MusicalNumberId };
        _db.CostumeNumbers.Add(row);
        await _db.SaveChangesAsync();
        return Ok(row);
    }

    // DELETE /api/costumenumbers?costumeId=&numberId=
    [HttpDelete]
    public async Task<IActionResult> Remove([FromQuery] int costumeId, [FromQuery] int numberId)
    {
        var row = await _db.CostumeNumbers.FirstOrDefaultAsync(
            cn => cn.CostumeId == costumeId && cn.MusicalNumberId == numberId);
        if (row is null || !await CanAccessNumber(numberId)) return NotFound();

        // Taking a costume out of a number leaves nobody wearing it there.
        var assignments = await _db.CostumeAssignments
            .Where(a => a.MusicalNumberId == numberId && a.CostumeId == costumeId).ToListAsync();
        foreach (var a in assignments) a.CostumeId = null;

        _db.CostumeNumbers.Remove(row);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<bool> CanAccessNumber(int numberId)
    {
        var pid = await _db.Numbers.Where(n => n.Id == numberId).Select(n => (int?)n.ProductionId).FirstOrDefaultAsync();
        return pid is not null && _tenant.CanAccessProduction(pid.Value);
    }
}
