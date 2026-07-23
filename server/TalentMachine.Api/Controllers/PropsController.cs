using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;
using TalentMachine.Api.Services;

namespace TalentMachine.Api.Controllers;

/// <summary>The prop catalog for a production, plus the backstage props PDF.</summary>
[ApiController]
[Route("api/[controller]")]
public class PropsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public PropsController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    // GET /api/props?productionId=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Prop>>> GetAll([FromQuery] int? productionId)
    {
        var query = _db.Props.AsQueryable();
        var accessible = _tenant.AccessibleProductionIds;
        if (accessible is not null)
            query = query.Where(p => accessible.Contains(p.ProductionId));
        if (productionId is not null)
            query = query.Where(p => p.ProductionId == productionId);
        return await query.OrderBy(p => p.OrderIndex).ThenBy(p => p.Name).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Prop>> Get(int id)
    {
        var prop = await _db.FindScopedAsync<Prop>(id);
        if (prop is null || !_tenant.CanAccessProduction(prop.ProductionId)) return NotFound();
        return prop;
    }

    [HttpPost]
    public async Task<ActionResult<Prop>> Create(Prop prop)
    {
        if (!_tenant.CanAccessProduction(prop.ProductionId)) return StatusCode(403);
        _db.Props.Add(prop);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = prop.Id }, prop);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Prop input)
    {
        if (id != input.Id) return BadRequest();
        if (!_tenant.CanAccessProduction(input.ProductionId)) return NotFound();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var prop = await _db.FindScopedAsync<Prop>(id);
        if (prop is null || !_tenant.CanAccessProduction(prop.ProductionId)) return NotFound();
        _db.Props.Remove(prop);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/props/pdf?productionId=
    [HttpGet("pdf")]
    public async Task<IActionResult> Pdf([FromQuery] int productionId, [FromServices] PropsPdfService pdf)
    {
        if (!_tenant.CanAccessProduction(productionId)) return NotFound();
        var production = await _db.FindScopedAsync<Production>(productionId);
        if (production is null) return NotFound();

        var props = await _db.Props.Where(p => p.ProductionId == productionId).ToListAsync();
        var propIds = props.Select(p => p.Id).ToList();

        var data = new PropSheetData
        {
            ProductionTitle = production.Title,
            Acts = await _db.Acts.Where(a => a.ProductionId == productionId).ToListAsync(),
            Scenes = await _db.Scenes.Where(s => s.ProductionId == productionId).ToListAsync(),
            Props = props,
            Assignments = await _db.PropAssignments.Where(a => propIds.Contains(a.PropId)).ToListAsync(),
        };

        var safeTitle = string.Join("-", production.Title.Split(Path.GetInvalidFileNameChars(),
            StringSplitOptions.RemoveEmptyEntries)).Replace(' ', '-').ToLowerInvariant();
        return File(pdf.Build(data), "application/pdf", $"props-{safeTitle}.pdf");
    }
}
