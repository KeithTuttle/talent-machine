using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>
/// A production's cast: which performers are in the show, and in which cast
/// group. One row per (production, performer) — enforced by a unique index and
/// a pre-check here.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CastMembershipsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public CastMembershipsController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    // GET /api/castmemberships?productionId= — a production's cast, with performers.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CastMembership>>> GetAll([FromQuery] int? productionId)
    {
        var query = _db.CastMemberships.Include(m => m.Performer).AsQueryable();
        var accessible = _tenant.AccessibleProductionIds;
        if (accessible is not null)
            query = query.Where(m => accessible.Contains(m.ProductionId));
        if (productionId is not null)
            query = query.Where(m => m.ProductionId == productionId);
        return await query.ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CastMembership>> Get(int id)
    {
        var membership = await _db.CastMemberships
            .Include(m => m.Performer)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (membership is null || !_tenant.CanAccessProduction(membership.ProductionId)) return NotFound();
        return membership;
    }

    // POST /api/castmemberships — add a performer to a production's cast.
    [HttpPost]
    public async Task<ActionResult<CastMembership>> Create(CastMembership input)
    {
        if (!_tenant.CanAccessProduction(input.ProductionId)) return StatusCode(403);
        var existing = await _db.CastMemberships.FirstOrDefaultAsync(
            m => m.ProductionId == input.ProductionId && m.PerformerId == input.PerformerId);
        if (existing is not null)
            return Conflict("That performer is already in this production's cast.");

        _db.CastMemberships.Add(input);
        await _db.SaveChangesAsync();
        await _db.Entry(input).Reference(m => m.Performer).LoadAsync();
        return CreatedAtAction(nameof(Get), new { id = input.Id }, input);
    }

    // PUT /api/castmemberships/{id} — e.g. move to another cast group.
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CastMembership input)
    {
        if (id != input.Id) return BadRequest();
        if (!_tenant.CanAccessProduction(input.ProductionId)) return NotFound();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    // DELETE /api/castmemberships/{id} — remove a performer from the show.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var membership = await _db.FindScopedAsync<CastMembership>(id);
        if (membership is null || !_tenant.CanAccessProduction(membership.ProductionId)) return NotFound();
        _db.CastMemberships.Remove(membership);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
