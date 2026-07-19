using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>Performer↔guardian links (siblings share a guardian record).</summary>
[ApiController]
[Route("api/[controller]")]
public class PerformerGuardiansController : ControllerBase
{
    private readonly AppDbContext _db;

    public PerformerGuardiansController(AppDbContext db) => _db = db;

    public record LinkRequest(int PerformerId, int GuardianId);

    // GET /api/performerguardians?performerId=|?guardianId= (no filter = all links)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PerformerGuardian>>> GetAll(
        [FromQuery] int? performerId, [FromQuery] int? guardianId)
    {
        var query = _db.PerformerGuardians.AsQueryable();
        if (performerId is not null)
            query = query.Where(l => l.PerformerId == performerId);
        if (guardianId is not null)
            query = query.Where(l => l.GuardianId == guardianId);
        return await query.ToListAsync();
    }

    // POST /api/performerguardians — link (idempotent).
    [HttpPost]
    public async Task<ActionResult<PerformerGuardian>> Add(LinkRequest input)
    {
        var existing = await _db.PerformerGuardians.FirstOrDefaultAsync(
            l => l.PerformerId == input.PerformerId && l.GuardianId == input.GuardianId);
        if (existing is not null) return Ok(existing);

        var link = new PerformerGuardian { PerformerId = input.PerformerId, GuardianId = input.GuardianId };
        _db.PerformerGuardians.Add(link);
        await _db.SaveChangesAsync();
        return Ok(link);
    }

    // DELETE /api/performerguardians?performerId=&guardianId=
    [HttpDelete]
    public async Task<IActionResult> Remove([FromQuery] int performerId, [FromQuery] int guardianId)
    {
        var link = await _db.PerformerGuardians.FirstOrDefaultAsync(
            l => l.PerformerId == performerId && l.GuardianId == guardianId);
        if (link is null) return NotFound();

        _db.PerformerGuardians.Remove(link);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
