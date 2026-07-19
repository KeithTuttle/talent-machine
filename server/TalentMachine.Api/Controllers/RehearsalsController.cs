using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;
using TalentMachine.Api.Services;

namespace TalentMachine.Api.Controllers;

/// <summary>
/// Rehearsal slots plus the schedule PDF and the AI suggestion endpoint.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RehearsalsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public RehearsalsController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public record BulkRequest(int ProductionId, List<Rehearsal> Rehearsals);
    public record SuggestRequest(int ProductionId, string? Prompt, DateOnly FromDate, DateOnly ToDate);
    public record SuggestResponse(bool Configured, bool Ok, List<SuggestedSlot> Slots);

    // GET /api/rehearsals?productionId=&from=&to=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Rehearsal>>> GetAll(
        [FromQuery] int? productionId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var query = _db.Rehearsals.AsQueryable();
        var accessible = _tenant.AccessibleProductionIds;
        if (accessible is not null)
            query = query.Where(r => accessible.Contains(r.ProductionId));
        if (productionId is not null)
            query = query.Where(r => r.ProductionId == productionId);
        if (from is not null)
            query = query.Where(r => r.Date >= from);
        if (to is not null)
            query = query.Where(r => r.Date <= to);
        return await query.OrderBy(r => r.Date).ThenBy(r => r.StartTime).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Rehearsal>> Get(int id)
    {
        var rehearsal = await _db.FindScopedAsync<Rehearsal>(id);
        if (rehearsal is null || !_tenant.CanAccessProduction(rehearsal.ProductionId)) return NotFound();
        return rehearsal;
    }

    [HttpPost]
    public async Task<ActionResult<Rehearsal>> Create(Rehearsal rehearsal)
    {
        if (!_tenant.CanAccessProduction(rehearsal.ProductionId)) return StatusCode(403);
        _db.Rehearsals.Add(rehearsal);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = rehearsal.Id }, rehearsal);
    }

    // POST /api/rehearsals/bulk — save an AI-suggested draft in one call.
    [HttpPost("bulk")]
    public async Task<ActionResult<IEnumerable<Rehearsal>>> CreateBulk(BulkRequest input)
    {
        if (!_tenant.CanAccessProduction(input.ProductionId)) return StatusCode(403);
        foreach (var r in input.Rehearsals)
        {
            r.Id = 0;
            r.ProductionId = input.ProductionId;
        }
        _db.Rehearsals.AddRange(input.Rehearsals);
        await _db.SaveChangesAsync();
        return Ok(input.Rehearsals);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Rehearsal input)
    {
        if (id != input.Id) return BadRequest();
        if (!_tenant.CanAccessProduction(input.ProductionId)) return NotFound();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var rehearsal = await _db.FindScopedAsync<Rehearsal>(id);
        if (rehearsal is null || !_tenant.CanAccessProduction(rehearsal.ProductionId)) return NotFound();
        _db.Rehearsals.Remove(rehearsal);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/rehearsals/pdf?productionId=&from=&to= — printable weekly schedule.
    [HttpGet("pdf")]
    public async Task<IActionResult> Pdf(
        [FromQuery] int productionId, [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromServices] RehearsalPdfService pdf)
    {
        if (!_tenant.CanAccessProduction(productionId)) return NotFound();
        var production = await _db.FindScopedAsync<Production>(productionId);
        if (production is null) return NotFound();

        var slots = await _db.Rehearsals
            .Where(r => r.ProductionId == productionId && r.Date >= from && r.Date <= to)
            .ToListAsync();
        var slotIds = slots.Select(s => s.Id).ToList();
        var numberIds = await _db.Numbers
            .Where(n => n.ProductionId == productionId).Select(n => n.Id).ToListAsync();

        var data = new RehearsalPdfData
        {
            ProductionTitle = production.Title,
            From = from,
            To = to,
            Slots = slots,
            Overrides = await _db.RehearsalAttendees.Where(a => slotIds.Contains(a.RehearsalId)).ToListAsync(),
            NumberCasts = await _db.NumberCasts.Where(c => numberIds.Contains(c.MusicalNumberId)).ToListAsync(),
            Numbers = await _db.Numbers.Where(n => n.ProductionId == productionId).ToListAsync(),
            Performers = await _db.Performers.ToListAsync(),
            Conflicts = await _db.Conflicts.Where(c => c.ProductionId == productionId).ToListAsync(),
        };

        var bytes = pdf.Build(data);
        return File(bytes, "application/pdf", $"rehearsals-{from:yyyy-MM-dd}.pdf");
    }

    // POST /api/rehearsals/suggest — AI schedule draft. Returns configured:false
    // when Gemini isn't set up (the ONLY case the client hides the feature).
    [HttpPost("suggest")]
    public async Task<ActionResult<SuggestResponse>> Suggest(
        SuggestRequest input, [FromServices] RehearsalAiService ai)
    {
        if (!_tenant.CanAccessProduction(input.ProductionId)) return StatusCode(403);
        if (!ai.IsConfigured) return new SuggestResponse(false, false, new());

        var production = await _db.FindScopedAsync<Production>(input.ProductionId);
        if (production is null) return NotFound();

        var numbers = await _db.Numbers
            .Where(n => n.ProductionId == input.ProductionId)
            .OrderBy(n => n.OrderIndex).ToListAsync();
        var numberIds = numbers.Select(n => n.Id).ToList();
        var numberCasts = await _db.NumberCasts
            .Where(c => numberIds.Contains(c.MusicalNumberId)).ToListAsync();
        var castIds = await _db.CastMemberships
            .Where(m => m.ProductionId == input.ProductionId).Select(m => m.PerformerId).ToListAsync();
        var cast = await _db.Performers.Where(p => castIds.Contains(p.Id)).ToListAsync();
        // Conflicts possibly active in the range: one-offs overlapping it + all weeklies.
        var conflicts = (await _db.Conflicts
                .Where(c => c.ProductionId == input.ProductionId).ToListAsync())
            .Where(c => c.Type == ConflictType.Weekly
                || ((c.EndDate ?? c.StartDate) >= input.FromDate && c.StartDate <= input.ToDate))
            .ToList();

        var result = await ai.SuggestAsync(new SuggestContext(
            production.Title, input.FromDate, input.ToDate, input.Prompt,
            numbers, numberCasts.ToLookup(c => c.MusicalNumberId, c => c.PerformerId),
            cast, conflicts));

        return new SuggestResponse(result.Configured, result.Ok, result.Slots);
    }
}
