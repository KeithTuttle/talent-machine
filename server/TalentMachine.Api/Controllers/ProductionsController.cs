using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductionsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductionsController(AppDbContext db) => _db = db;

    // GET /api/productions?seasonId= — all productions, optionally one season's.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Production>>> GetAll(
        [FromQuery] int? seasonId, [FromQuery] bool includeArchived = false)
    {
        var query = _db.Productions.AsQueryable();
        if (seasonId is not null)
            query = query.Where(p => p.SeasonId == seasonId);
        if (!includeArchived)
            query = query.Where(p => !p.IsArchived);
        return await query.OrderBy(p => p.Title).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Production>> Get(int id)
    {
        var production = await _db.FindScopedAsync<Production>(id);
        return production is null ? NotFound() : production;
    }

    [HttpPost]
    public async Task<ActionResult<Production>> Create(Production production)
    {
        _db.Productions.Add(production);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = production.Id }, production);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Production input)
    {
        if (id != input.Id) return BadRequest();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var production = await _db.FindScopedAsync<Production>(id);
        if (production is null) return NotFound();
        _db.Productions.Remove(production);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
