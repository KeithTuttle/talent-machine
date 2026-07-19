using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>
/// Shows. Owners see and manage everything; Members only see productions they've
/// been granted (show-level collaboration), and cannot create or delete shows.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProductionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public ProductionsController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    // GET /api/productions?seasonId= — accessible productions, optionally one season's.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Production>>> GetAll(
        [FromQuery] int? seasonId, [FromQuery] bool includeArchived = false)
    {
        var query = _db.Productions.AsQueryable();
        var accessible = _tenant.AccessibleProductionIds;
        if (accessible is not null)
            query = query.Where(p => accessible.Contains(p.Id));
        if (seasonId is not null)
            query = query.Where(p => p.SeasonId == seasonId);
        if (!includeArchived)
            query = query.Where(p => !p.IsArchived);
        return await query.OrderBy(p => p.Title).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Production>> Get(int id)
    {
        if (!_tenant.CanAccessProduction(id)) return NotFound();
        var production = await _db.FindScopedAsync<Production>(id);
        return production is null ? NotFound() : production;
    }

    [HttpPost]
    public async Task<ActionResult<Production>> Create(Production production)
    {
        if (!_tenant.IsOwner()) return StatusCode(403);
        _db.Productions.Add(production);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = production.Id }, production);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Production input)
    {
        if (id != input.Id) return BadRequest();
        if (!_tenant.CanAccessProduction(id)) return NotFound();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!_tenant.IsOwner()) return StatusCode(403);
        var production = await _db.FindScopedAsync<Production>(id);
        if (production is null) return NotFound();
        _db.Productions.Remove(production);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
