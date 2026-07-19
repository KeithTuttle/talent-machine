using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>Per-number casting: which performers appear in which musical number.</summary>
[ApiController]
[Route("api/[controller]")]
public class NumberCastController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public NumberCastController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public record CastRequest(int MusicalNumberId, int PerformerId);

    // GET /api/numbercast?numberId=      (one number's cast)
    // GET /api/numbercast?productionId=  (all casting in a production, for the planner grid)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NumberCast>>> GetAll(
        [FromQuery] int? numberId, [FromQuery] int? productionId)
    {
        var query = _db.NumberCasts.AsQueryable();
        var accessible = _tenant.AccessibleProductionIds;
        if (accessible is not null)
            query = query.Where(c => c.MusicalNumber != null && accessible.Contains(c.MusicalNumber.ProductionId));
        if (numberId is not null)
            query = query.Where(c => c.MusicalNumberId == numberId);
        if (productionId is not null)
            query = query.Where(c => c.MusicalNumber != null && c.MusicalNumber.ProductionId == productionId);
        return await query.ToListAsync();
    }

    // POST /api/numbercast — cast a performer in a number (idempotent).
    [HttpPost]
    public async Task<ActionResult<NumberCast>> Add(CastRequest input)
    {
        // Resolve the number's production for the show-level access check
        // (tenant-filtered, so an out-of-tenant number isn't found either).
        var number = await _db.Numbers.FirstOrDefaultAsync(n => n.Id == input.MusicalNumberId);
        if (number is null || !_tenant.CanAccessProduction(number.ProductionId)) return NotFound();

        var existing = await _db.NumberCasts.FirstOrDefaultAsync(
            c => c.MusicalNumberId == input.MusicalNumberId && c.PerformerId == input.PerformerId);
        if (existing is not null) return Ok(existing);

        var cast = new NumberCast { MusicalNumberId = input.MusicalNumberId, PerformerId = input.PerformerId };
        _db.NumberCasts.Add(cast);
        await _db.SaveChangesAsync();
        return Ok(cast);
    }

    // DELETE /api/numbercast?numberId=&performerId= — remove a performer from a number.
    [HttpDelete]
    public async Task<IActionResult> Remove([FromQuery] int numberId, [FromQuery] int performerId)
    {
        // Tenant-filtered lookup: an out-of-tenant row simply isn't found.
        var cast = await _db.NumberCasts
            .Include(c => c.MusicalNumber)
            .FirstOrDefaultAsync(c => c.MusicalNumberId == numberId && c.PerformerId == performerId);
        if (cast is null || cast.MusicalNumber is null
            || !_tenant.CanAccessProduction(cast.MusicalNumber.ProductionId)) return NotFound();

        _db.NumberCasts.Remove(cast);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
