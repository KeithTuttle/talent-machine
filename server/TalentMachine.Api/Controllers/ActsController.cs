using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>Acts of a show ("Act 1", "Act 2") — the running-order sections.</summary>
[ApiController]
[Route("api/[controller]")]
public class ActsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public ActsController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    // GET /api/acts?productionId=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Act>>> GetAll([FromQuery] int? productionId)
    {
        var query = _db.Acts.AsQueryable();
        var accessible = _tenant.AccessibleProductionIds;
        if (accessible is not null)
            query = query.Where(a => accessible.Contains(a.ProductionId));
        if (productionId is not null)
            query = query.Where(a => a.ProductionId == productionId);
        return await query.OrderBy(a => a.OrderIndex).ThenBy(a => a.Id).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Act>> Get(int id)
    {
        var act = await _db.FindScopedAsync<Act>(id);
        if (act is null || !_tenant.CanAccessProduction(act.ProductionId)) return NotFound();
        return act;
    }

    [HttpPost]
    public async Task<ActionResult<Act>> Create(Act act)
    {
        if (!_tenant.CanAccessProduction(act.ProductionId)) return StatusCode(403);
        _db.Acts.Add(act);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = act.Id }, act);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Act input)
    {
        if (id != input.Id) return BadRequest();
        if (!_tenant.CanAccessProduction(input.ProductionId)) return NotFound();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var act = await _db.FindScopedAsync<Act>(id);
        if (act is null || !_tenant.CanAccessProduction(act.ProductionId)) return NotFound();
        _db.Acts.Remove(act);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
