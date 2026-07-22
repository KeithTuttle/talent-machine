using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>Scenes in a production's book — the story units songs sit inside.</summary>
[ApiController]
[Route("api/[controller]")]
public class ScenesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public ScenesController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    // GET /api/scenes?productionId=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Scene>>> GetAll([FromQuery] int? productionId)
    {
        var query = _db.Scenes.AsQueryable();
        var accessible = _tenant.AccessibleProductionIds;
        if (accessible is not null)
            query = query.Where(s => accessible.Contains(s.ProductionId));
        if (productionId is not null)
            query = query.Where(s => s.ProductionId == productionId);
        return await query.OrderBy(s => s.OrderIndex).ThenBy(s => s.Name).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Scene>> Get(int id)
    {
        var scene = await _db.FindScopedAsync<Scene>(id);
        if (scene is null || !_tenant.CanAccessProduction(scene.ProductionId)) return NotFound();
        return scene;
    }

    [HttpPost]
    public async Task<ActionResult<Scene>> Create(Scene scene)
    {
        if (!_tenant.CanAccessProduction(scene.ProductionId)) return StatusCode(403);
        _db.Scenes.Add(scene);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = scene.Id }, scene);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Scene input)
    {
        if (id != input.Id) return BadRequest();
        if (!_tenant.CanAccessProduction(input.ProductionId)) return NotFound();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var scene = await _db.FindScopedAsync<Scene>(id);
        if (scene is null || !_tenant.CanAccessProduction(scene.ProductionId)) return NotFound();
        _db.Scenes.Remove(scene);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
