using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>Age/ability groups within a production (see LevelGroup model).</summary>
[ApiController]
[Route("api/[controller]")]
public class LevelGroupsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public LevelGroupsController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    // GET /api/levelgroups?productionId=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LevelGroup>>> GetAll([FromQuery] int? productionId)
    {
        var query = _db.LevelGroups.AsQueryable();
        var accessible = _tenant.AccessibleProductionIds;
        if (accessible is not null)
            query = query.Where(g => accessible.Contains(g.ProductionId));
        if (productionId is not null)
            query = query.Where(g => g.ProductionId == productionId);
        return await query.OrderBy(g => g.OrderIndex).ThenBy(g => g.Name).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LevelGroup>> Get(int id)
    {
        var group = await _db.FindScopedAsync<LevelGroup>(id);
        if (group is null || !_tenant.CanAccessProduction(group.ProductionId)) return NotFound();
        return group;
    }

    [HttpPost]
    public async Task<ActionResult<LevelGroup>> Create(LevelGroup group)
    {
        if (!_tenant.CanAccessProduction(group.ProductionId)) return StatusCode(403);
        _db.LevelGroups.Add(group);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = group.Id }, group);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, LevelGroup input)
    {
        if (id != input.Id) return BadRequest();
        if (!_tenant.CanAccessProduction(input.ProductionId)) return NotFound();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var group = await _db.FindScopedAsync<LevelGroup>(id);
        if (group is null || !_tenant.CanAccessProduction(group.ProductionId)) return NotFound();
        _db.LevelGroups.Remove(group);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
