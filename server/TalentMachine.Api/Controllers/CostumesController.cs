using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;
using TalentMachine.Api.Services;

namespace TalentMachine.Api.Controllers;

/// <summary>
/// The production's costume catalog (reusable across numbers) plus the printable
/// backstage sheets. Mirrors PropsController: catalog CRUD here, per-number use
/// on CostumeNumbersController, per-kid detail on CostumeAssignmentsController.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CostumesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public CostumesController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    // GET /api/costumes?productionId=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Costume>>> GetAll([FromQuery] int? productionId)
    {
        var query = _db.Costumes.AsQueryable();
        var accessible = _tenant.AccessibleProductionIds;
        if (accessible is not null) query = query.Where(c => accessible.Contains(c.ProductionId));
        if (productionId is not null) query = query.Where(c => c.ProductionId == productionId);
        return await query.OrderBy(c => c.OrderIndex).ThenBy(c => c.Name).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Costume>> Get(int id)
    {
        var costume = await _db.FindScopedAsync<Costume>(id);
        if (costume is null || !_tenant.CanAccessProduction(costume.ProductionId)) return NotFound();
        return costume;
    }

    [HttpPost]
    public async Task<ActionResult<Costume>> Create(Costume costume)
    {
        if (!_tenant.CanAccessProduction(costume.ProductionId)) return StatusCode(403);
        if (costume.OrderIndex == 0)
        {
            var max = await _db.Costumes
                .Where(c => c.ProductionId == costume.ProductionId)
                .MaxAsync(c => (int?)c.OrderIndex) ?? 0;
            costume.OrderIndex = max + 1;
        }
        _db.Costumes.Add(costume);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = costume.Id }, costume);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Costume input)
    {
        if (id != input.Id) return BadRequest();
        if (!_tenant.CanAccessProduction(input.ProductionId)) return NotFound();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var costume = await _db.FindScopedAsync<Costume>(id);
        if (costume is null || !_tenant.CanAccessProduction(costume.ProductionId)) return NotFound();
        _db.Costumes.Remove(costume);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/costumes/pdf?productionId=
    [HttpGet("pdf")]
    public async Task<IActionResult> Pdf([FromQuery] int productionId, [FromServices] CostumePdfService pdf)
    {
        if (!_tenant.CanAccessProduction(productionId)) return NotFound();
        var production = await _db.FindScopedAsync<Production>(productionId);
        if (production is null) return NotFound();

        var numbers = await _db.Numbers.Where(n => n.ProductionId == productionId).ToListAsync();
        var numberIds = numbers.Select(n => n.Id).ToList();

        var data = new CostumeSheetData
        {
            ProductionTitle = production.Title,
            Acts = await _db.Acts.Where(a => a.ProductionId == productionId).ToListAsync(),
            Numbers = numbers,
            Costumes = await _db.Costumes.Where(c => c.ProductionId == productionId).ToListAsync(),
            CostumeNumbers = await _db.CostumeNumbers.Where(cn => numberIds.Contains(cn.MusicalNumberId)).ToListAsync(),
            Assignments = await _db.CostumeAssignments.Where(a => numberIds.Contains(a.MusicalNumberId)).ToListAsync(),
            NumberCasts = await _db.NumberCasts.Where(c => numberIds.Contains(c.MusicalNumberId)).ToListAsync(),
            Performers = await _db.Performers.ToListAsync(),
        };

        return File(pdf.Build(data), "application/pdf", $"costumes-{SafeTitle(production.Title)}.pdf");
    }

    // GET /api/costumes/quickchanges/pdf?productionId= — the dressers' wing sheet.
    [HttpGet("quickchanges/pdf")]
    public async Task<IActionResult> QuickChangesPdf(
        [FromQuery] int productionId, [FromServices] QuickChangePdfService pdf)
    {
        if (!_tenant.CanAccessProduction(productionId)) return NotFound();
        var production = await _db.FindScopedAsync<Production>(productionId);
        if (production is null) return NotFound();

        var numbers = await _db.Numbers.Where(n => n.ProductionId == productionId).ToListAsync();
        var numberIds = numbers.Select(n => n.Id).ToList();

        var data = new QuickChangeData
        {
            ProductionTitle = production.Title,
            Acts = await _db.Acts.Where(a => a.ProductionId == productionId).ToListAsync(),
            Numbers = numbers,
            NumberCasts = await _db.NumberCasts.Where(c => numberIds.Contains(c.MusicalNumberId)).ToListAsync(),
            Assignments = await _db.CostumeAssignments.Where(a => numberIds.Contains(a.MusicalNumberId)).ToListAsync(),
            Costumes = await _db.Costumes.Where(c => c.ProductionId == productionId).ToListAsync(),
            CostumeNumbers = await _db.CostumeNumbers.Where(cn => numberIds.Contains(cn.MusicalNumberId)).ToListAsync(),
            Performers = await _db.Performers.ToListAsync(),
        };

        return File(pdf.Build(data), "application/pdf", $"quick-changes-{SafeTitle(production.Title)}.pdf");
    }

    // GET /api/costumes/plot/pdf?productionId= — one block per kid, for parents.
    [HttpGet("plot/pdf")]
    public async Task<IActionResult> PlotPdf(
        [FromQuery] int productionId, [FromServices] CostumePlotPdfService pdf)
    {
        if (!_tenant.CanAccessProduction(productionId)) return NotFound();
        var production = await _db.FindScopedAsync<Production>(productionId);
        if (production is null) return NotFound();

        var numbers = await _db.Numbers.Where(n => n.ProductionId == productionId).ToListAsync();
        var numberIds = numbers.Select(n => n.Id).ToList();

        var data = new CostumePlotData
        {
            ProductionTitle = production.Title,
            Acts = await _db.Acts.Where(a => a.ProductionId == productionId).ToListAsync(),
            Numbers = numbers,
            NumberCasts = await _db.NumberCasts.Where(c => numberIds.Contains(c.MusicalNumberId)).ToListAsync(),
            Assignments = await _db.CostumeAssignments.Where(a => numberIds.Contains(a.MusicalNumberId)).ToListAsync(),
            Costumes = await _db.Costumes.Where(c => c.ProductionId == productionId).ToListAsync(),
            CostumeNumbers = await _db.CostumeNumbers.Where(cn => numberIds.Contains(cn.MusicalNumberId)).ToListAsync(),
            Performers = await _db.Performers.ToListAsync(),
            Cast = await _db.CastMemberships.Where(m => m.ProductionId == productionId).ToListAsync(),
        };

        return File(pdf.Build(data), "application/pdf", $"costume-plot-{SafeTitle(production.Title)}.pdf");
    }

    private static string SafeTitle(string title) =>
        string.Join("-", title.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))
            .Replace(' ', '-').ToLowerInvariant();
}
