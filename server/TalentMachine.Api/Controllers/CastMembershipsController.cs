using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>
/// A production's cast: who's in the show, and in which cast group. One row per
/// (production, person) — enforced by a unique index and a pre-check here.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CastMembershipsController : ControllerBase
{
    private readonly AppDbContext _db;

    public CastMembershipsController(AppDbContext db) => _db = db;

    // GET /api/castmemberships?productionId= — a production's cast, with people.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CastMembership>>> GetAll([FromQuery] int? productionId)
    {
        var query = _db.CastMemberships.Include(m => m.Person).AsQueryable();
        if (productionId is not null)
            query = query.Where(m => m.ProductionId == productionId);
        return await query.ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CastMembership>> Get(int id)
    {
        var membership = await _db.CastMemberships
            .Include(m => m.Person)
            .FirstOrDefaultAsync(m => m.Id == id);
        return membership is null ? NotFound() : membership;
    }

    // POST /api/castmemberships — add a person to a production's cast.
    [HttpPost]
    public async Task<ActionResult<CastMembership>> Create(CastMembership input)
    {
        var existing = await _db.CastMemberships.FirstOrDefaultAsync(
            m => m.ProductionId == input.ProductionId && m.PersonId == input.PersonId);
        if (existing is not null)
            return Conflict("That person is already in this production's cast.");

        _db.CastMemberships.Add(input);
        await _db.SaveChangesAsync();
        await _db.Entry(input).Reference(m => m.Person).LoadAsync();
        return CreatedAtAction(nameof(Get), new { id = input.Id }, input);
    }

    // PUT /api/castmemberships/{id} — e.g. move to another cast group.
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CastMembership input)
    {
        if (id != input.Id) return BadRequest();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    // DELETE /api/castmemberships/{id} — remove a person from the show.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var membership = await _db.FindScopedAsync<CastMembership>(id);
        if (membership is null) return NotFound();
        _db.CastMemberships.Remove(membership);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
