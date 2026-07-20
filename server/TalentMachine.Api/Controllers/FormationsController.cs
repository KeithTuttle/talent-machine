using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;
using TalentMachine.Api.Services;

namespace TalentMachine.Api.Controllers;

/// <summary>Stage formations per number (persisted history) + AI suggestions.</summary>
[ApiController]
[Route("api/[controller]")]
public class FormationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public FormationsController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public record SuggestRequest(List<FormationDancer> Dancers, string? Description);
    public record SuggestResponse(bool Configured, bool Ok, Dictionary<int, FormationCoord> Coordinates);

    // GET /api/formations?numberId=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Formation>>> GetAll([FromQuery] int numberId)
    {
        if (!await CanAccessNumber(numberId)) return NotFound();
        return await _db.Formations
            .Where(f => f.MusicalNumberId == numberId)
            .OrderBy(f => f.OrderIndex).ThenBy(f => f.Id)
            .ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Formation>> Get(int id)
    {
        var formation = await _db.FindScopedAsync<Formation>(id);
        if (formation is null || !await CanAccessNumber(formation.MusicalNumberId)) return NotFound();
        return formation;
    }

    [HttpPost]
    public async Task<ActionResult<Formation>> Create(Formation formation)
    {
        if (!await CanAccessNumber(formation.MusicalNumberId)) return StatusCode(403);
        _db.Formations.Add(formation);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = formation.Id }, formation);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Formation input)
    {
        if (id != input.Id) return BadRequest();
        if (!await CanAccessNumber(input.MusicalNumberId)) return NotFound();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var formation = await _db.FindScopedAsync<Formation>(id);
        if (formation is null || !await CanAccessNumber(formation.MusicalNumberId)) return NotFound();
        _db.Formations.Remove(formation);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/formations/suggest — stateless AI placement (client applies + saves).
    // configured:false is the only signal that hides the feature.
    [HttpPost("suggest")]
    public async Task<ActionResult<SuggestResponse>> Suggest(
        SuggestRequest input, [FromServices] FormationAiService ai)
    {
        var result = await ai.SuggestAsync(input.Dancers, input.Description);
        return new SuggestResponse(result.Configured, result.Ok, result.Coordinates);
    }

    private async Task<bool> CanAccessNumber(int numberId)
    {
        var pid = await _db.Numbers.Where(n => n.Id == numberId).Select(n => (int?)n.ProductionId).FirstOrDefaultAsync();
        return pid is not null && _tenant.CanAccessProduction(pid.Value);
    }
}
