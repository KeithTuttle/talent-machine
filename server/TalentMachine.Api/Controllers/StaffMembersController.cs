using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>
/// The company directory of non-performer people (creative team + invitees).
/// Tenant-wide, like performers, so returning people are reused across shows.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StaffMembersController : ControllerBase
{
    private readonly AppDbContext _db;

    public StaffMembersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<StaffMember>>> GetAll()
        => await _db.StaffMembers.OrderBy(s => s.Name).ToListAsync();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StaffMember>> Get(int id)
    {
        var member = await _db.FindScopedAsync<StaffMember>(id);
        return member is null ? NotFound() : member;
    }

    [HttpPost]
    public async Task<ActionResult<StaffMember>> Create(StaffMember member)
    {
        _db.StaffMembers.Add(member);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = member.Id }, member);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, StaffMember input)
    {
        if (id != input.Id) return BadRequest();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var member = await _db.FindScopedAsync<StaffMember>(id);
        if (member is null) return NotFound();
        _db.StaffMembers.Remove(member);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
