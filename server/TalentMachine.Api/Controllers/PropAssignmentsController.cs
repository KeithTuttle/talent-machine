using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>Per-scene prop usage: preset location, handler, strike-to, notes.</summary>
[ApiController]
[Route("api/[controller]")]
public class PropAssignmentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public PropAssignmentsController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    // GET /api/propassignments?productionId=|?sceneId=|?propId=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PropAssignment>>> GetAll(
        [FromQuery] int? productionId, [FromQuery] int? sceneId, [FromQuery] int? propId)
    {
        var query = _db.PropAssignments.AsQueryable();
        var accessible = _tenant.AccessibleProductionIds;
        if (accessible is not null)
            query = query.Where(a => a.Prop != null && accessible.Contains(a.Prop.ProductionId));
        if (productionId is not null)
            query = query.Where(a => a.Prop != null && a.Prop.ProductionId == productionId);
        if (sceneId is not null)
            query = query.Where(a => a.SceneId == sceneId);
        if (propId is not null)
            query = query.Where(a => a.PropId == propId);
        return await query.ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PropAssignment>> Get(int id)
    {
        var a = await _db.FindScopedAsync<PropAssignment>(id);
        if (a is null || !await CanAccessAsync(a.PropId)) return NotFound();
        return a;
    }

    [HttpPost]
    public async Task<ActionResult<PropAssignment>> Create(PropAssignment input)
    {
        if (!await CanAccessAsync(input.PropId)) return NotFound();
        _db.PropAssignments.Add(input);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = input.Id }, input);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, PropAssignment input)
    {
        if (id != input.Id) return BadRequest();
        if (!await CanAccessAsync(input.PropId)) return NotFound();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var a = await _db.FindScopedAsync<PropAssignment>(id);
        if (a is null || !await CanAccessAsync(a.PropId)) return NotFound();
        _db.PropAssignments.Remove(a);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Resolve the assignment's production via its prop for the access check.</summary>
    private async Task<bool> CanAccessAsync(int propId)
    {
        var prop = await _db.Props.FirstOrDefaultAsync(p => p.Id == propId);
        return prop is not null && _tenant.CanAccessProduction(prop.ProductionId);
    }
}
