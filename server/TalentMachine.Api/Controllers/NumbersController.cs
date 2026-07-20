using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;
using TalentMachine.Api.Services;

namespace TalentMachine.Api.Controllers;

/// <summary>Musical numbers within a production, kept in running order.</summary>
[ApiController]
[Route("api/[controller]")]
public class NumbersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public NumbersController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public record ReorderRequest(int ProductionId, List<int> OrderedIds);

    // GET /api/numbers?productionId=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MusicalNumber>>> GetAll([FromQuery] int? productionId)
    {
        var query = _db.Numbers.AsQueryable();
        var accessible = _tenant.AccessibleProductionIds;
        if (accessible is not null)
            query = query.Where(n => accessible.Contains(n.ProductionId));
        if (productionId is not null)
            query = query.Where(n => n.ProductionId == productionId);
        return await query.OrderBy(n => n.OrderIndex).ThenBy(n => n.Id).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MusicalNumber>> Get(int id)
    {
        var number = await _db.FindScopedAsync<MusicalNumber>(id);
        if (number is null || !_tenant.CanAccessProduction(number.ProductionId)) return NotFound();
        return number;
    }

    [HttpPost]
    public async Task<ActionResult<MusicalNumber>> Create(MusicalNumber number)
    {
        if (!_tenant.CanAccessProduction(number.ProductionId)) return StatusCode(403);
        // New numbers go to the end of the running order unless told otherwise.
        if (number.OrderIndex == 0)
        {
            var max = await _db.Numbers
                .Where(n => n.ProductionId == number.ProductionId)
                .MaxAsync(n => (int?)n.OrderIndex) ?? 0;
            number.OrderIndex = max + 1;
        }
        _db.Numbers.Add(number);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = number.Id }, number);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, MusicalNumber input)
    {
        if (id != input.Id) return BadRequest();
        if (!_tenant.CanAccessProduction(input.ProductionId)) return NotFound();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    // PUT /api/numbers/reorder — set a production's running order in one call.
    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder(ReorderRequest input)
    {
        if (!_tenant.CanAccessProduction(input.ProductionId)) return StatusCode(403);
        var numbers = await _db.Numbers
            .Where(n => n.ProductionId == input.ProductionId)
            .ToListAsync();
        var byId = numbers.ToDictionary(n => n.Id);
        var order = 1;
        foreach (var id in input.OrderedIds)
        {
            if (byId.TryGetValue(id, out var number))
                number.OrderIndex = order++;
        }
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/numbers/showorder/pdf?productionId= — printable running order.
    [HttpGet("showorder/pdf")]
    public async Task<IActionResult> ShowOrderPdf(
        [FromQuery] int productionId, [FromServices] ShowOrderPdfService pdf)
    {
        if (!_tenant.CanAccessProduction(productionId)) return NotFound();
        var production = await _db.FindScopedAsync<Production>(productionId);
        if (production is null) return NotFound();

        var numbers = await _db.Numbers.Where(n => n.ProductionId == productionId).ToListAsync();
        var numberIds = numbers.Select(n => n.Id).ToList();

        var data = new ShowOrderData
        {
            ProductionTitle = production.Title,
            OpeningDate = production.OpeningDate,
            Acts = await _db.Acts.Where(a => a.ProductionId == productionId).ToListAsync(),
            Numbers = numbers,
            NumberCasts = await _db.NumberCasts.Where(c => numberIds.Contains(c.MusicalNumberId)).ToListAsync(),
            Performers = await _db.Performers.ToListAsync(),
            StaffNames = await _db.StaffMembers.ToDictionaryAsync(s => s.Id, s => s.Name),
        };

        var safeTitle = string.Join("-", production.Title.Split(Path.GetInvalidFileNameChars(),
            StringSplitOptions.RemoveEmptyEntries)).Replace(' ', '-').ToLowerInvariant();
        return File(pdf.Build(data), "application/pdf", $"show-order-{safeTitle}.pdf");
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var number = await _db.FindScopedAsync<MusicalNumber>(id);
        if (number is null || !_tenant.CanAccessProduction(number.ProductionId)) return NotFound();
        _db.Numbers.Remove(number);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
