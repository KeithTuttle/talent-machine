using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>
/// The company's performer roster. Tenant-wide (not per-show) so casting history
/// spans years; visible to every member because casting a show requires it.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PerformersController : ControllerBase
{
    private readonly AppDbContext _db;

    public PerformersController(AppDbContext db) => _db = db;

    // GET /api/performers — active performers only, unless ?includeInactive=true.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Performer>>> GetAll([FromQuery] bool includeInactive = false)
    {
        var query = _db.Performers.AsQueryable();
        if (!includeInactive)
            query = query.Where(p => p.IsActive);
        return await query.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Performer>> Get(int id)
    {
        var performer = await _db.FindScopedAsync<Performer>(id);
        return performer is null ? NotFound() : performer;
    }

    [HttpPost]
    public async Task<ActionResult<Performer>> Create(Performer performer)
    {
        _db.Performers.Add(performer);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = performer.Id }, performer);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Performer input)
    {
        if (id != input.Id) return BadRequest();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var performer = await _db.FindScopedAsync<Performer>(id);
        if (performer is null) return NotFound();
        _db.Performers.Remove(performer);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
