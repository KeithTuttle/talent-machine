using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>
/// Parent/guardian records. Tenant-wide (like performers) so siblings share one
/// record across shows and years.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class GuardiansController : ControllerBase
{
    private readonly AppDbContext _db;

    public GuardiansController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Guardian>>> GetAll()
        => await _db.Guardians.OrderBy(g => g.Name).ToListAsync();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Guardian>> Get(int id)
    {
        var guardian = await _db.FindScopedAsync<Guardian>(id);
        return guardian is null ? NotFound() : guardian;
    }

    [HttpPost]
    public async Task<ActionResult<Guardian>> Create(Guardian guardian)
    {
        _db.Guardians.Add(guardian);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = guardian.Id }, guardian);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Guardian input)
    {
        if (id != input.Id) return BadRequest();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var guardian = await _db.FindScopedAsync<Guardian>(id);
        if (guardian is null) return NotFound();
        _db.Guardians.Remove(guardian);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
