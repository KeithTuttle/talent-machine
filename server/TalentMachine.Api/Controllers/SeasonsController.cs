using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SeasonsController : ControllerBase
{
    private readonly AppDbContext _db;

    public SeasonsController(AppDbContext db) => _db = db;

    // GET /api/seasons — active seasons only, unless ?includeArchived=true.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Season>>> GetAll([FromQuery] bool includeArchived = false)
    {
        var query = _db.Seasons.AsQueryable();
        if (!includeArchived)
            query = query.Where(s => !s.IsArchived);
        return await query.OrderByDescending(s => s.Year).ThenBy(s => s.Name).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Season>> Get(int id)
    {
        var season = await _db.FindScopedAsync<Season>(id);
        return season is null ? NotFound() : season;
    }

    [HttpPost]
    public async Task<ActionResult<Season>> Create(Season season)
    {
        _db.Seasons.Add(season);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = season.Id }, season);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Season input)
    {
        if (id != input.Id) return BadRequest();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var season = await _db.FindScopedAsync<Season>(id);
        if (season is null) return NotFound();
        _db.Seasons.Remove(season);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
