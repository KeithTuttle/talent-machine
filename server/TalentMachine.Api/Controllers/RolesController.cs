using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>Named characters in a production, optionally assigned to a performer.</summary>
[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public RolesController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    // GET /api/roles?productionId=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Role>>> GetAll([FromQuery] int? productionId)
    {
        var query = _db.Roles.AsQueryable();
        var accessible = _tenant.AccessibleProductionIds;
        if (accessible is not null)
            query = query.Where(r => accessible.Contains(r.ProductionId));
        if (productionId is not null)
            query = query.Where(r => r.ProductionId == productionId);
        return await query.OrderBy(r => r.OrderIndex).ThenBy(r => r.Name).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Role>> Get(int id)
    {
        var role = await _db.FindScopedAsync<Role>(id);
        if (role is null || !_tenant.CanAccessProduction(role.ProductionId)) return NotFound();
        return role;
    }

    [HttpPost]
    public async Task<ActionResult<Role>> Create(Role role)
    {
        if (!_tenant.CanAccessProduction(role.ProductionId)) return StatusCode(403);
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = role.Id }, role);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Role input)
    {
        if (id != input.Id) return BadRequest();
        if (!_tenant.CanAccessProduction(input.ProductionId)) return NotFound();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var role = await _db.FindScopedAsync<Role>(id);
        if (role is null || !_tenant.CanAccessProduction(role.ProductionId)) return NotFound();
        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
