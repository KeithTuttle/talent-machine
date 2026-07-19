using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>
/// Seasons are lightweight metadata: readable by every member (the picker needs
/// them), but only Owners shape the calendar (create/edit/archive/delete).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SeasonsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public SeasonsController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

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
        if (!_tenant.IsOwner()) return StatusCode(403);
        _db.Seasons.Add(season);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = season.Id }, season);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Season input)
    {
        if (id != input.Id) return BadRequest();
        if (!_tenant.IsOwner()) return StatusCode(403);
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!_tenant.IsOwner()) return StatusCode(403);
        var season = await _db.FindScopedAsync<Season>(id);
        if (season is null) return NotFound();
        _db.Seasons.Remove(season);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
