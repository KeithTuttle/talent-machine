using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>Per-number casting: which people perform in which musical number.</summary>
[ApiController]
[Route("api/[controller]")]
public class NumberCastController : ControllerBase
{
    private readonly AppDbContext _db;

    public NumberCastController(AppDbContext db) => _db = db;

    public record CastRequest(int MusicalNumberId, int PersonId);

    // GET /api/numbercast?numberId=      (one number's cast)
    // GET /api/numbercast?productionId=  (all casting in a production, for the planner grid)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NumberCast>>> GetAll(
        [FromQuery] int? numberId, [FromQuery] int? productionId)
    {
        var query = _db.NumberCasts.AsQueryable();
        if (numberId is not null)
            query = query.Where(c => c.MusicalNumberId == numberId);
        if (productionId is not null)
            query = query.Where(c => c.MusicalNumber != null && c.MusicalNumber.ProductionId == productionId);
        return await query.ToListAsync();
    }

    // POST /api/numbercast — cast a person in a number (idempotent).
    [HttpPost]
    public async Task<ActionResult<NumberCast>> Add(CastRequest input)
    {
        var existing = await _db.NumberCasts.FirstOrDefaultAsync(
            c => c.MusicalNumberId == input.MusicalNumberId && c.PersonId == input.PersonId);
        if (existing is not null) return Ok(existing);

        var cast = new NumberCast { MusicalNumberId = input.MusicalNumberId, PersonId = input.PersonId };
        _db.NumberCasts.Add(cast);
        await _db.SaveChangesAsync();
        return Ok(cast);
    }

    // DELETE /api/numbercast?numberId=&personId= — remove a person from a number.
    [HttpDelete]
    public async Task<IActionResult> Remove([FromQuery] int numberId, [FromQuery] int personId)
    {
        // Tenant-filtered lookup: an out-of-tenant row simply isn't found.
        var cast = await _db.NumberCasts.FirstOrDefaultAsync(
            c => c.MusicalNumberId == numberId && c.PersonId == personId);
        if (cast is null) return NotFound();

        _db.NumberCasts.Remove(cast);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
