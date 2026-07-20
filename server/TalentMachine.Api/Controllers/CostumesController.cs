using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;
using TalentMachine.Api.Services;

namespace TalentMachine.Api.Controllers;

/// <summary>The printable backstage costume sheet for a production.</summary>
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
            Pieces = await _db.CostumePieces.Where(p => numberIds.Contains(p.MusicalNumberId)).ToListAsync(),
            Assignments = await _db.CostumeAssignments.Where(a => numberIds.Contains(a.MusicalNumberId)).ToListAsync(),
            NumberCasts = await _db.NumberCasts.Where(c => numberIds.Contains(c.MusicalNumberId)).ToListAsync(),
            Performers = await _db.Performers.ToListAsync(),
        };

        var safeTitle = string.Join("-", production.Title.Split(Path.GetInvalidFileNameChars(),
            StringSplitOptions.RemoveEmptyEntries)).Replace(' ', '-').ToLowerInvariant();
        return File(pdf.Build(data), "application/pdf", $"costumes-{safeTitle}.pdf");
    }
}
