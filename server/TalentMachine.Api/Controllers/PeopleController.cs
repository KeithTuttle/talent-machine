using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PeopleController : ControllerBase
{
    private readonly AppDbContext _db;

    public PeopleController(AppDbContext db) => _db = db;

    // GET /api/people — active people only, unless ?includeInactive=true.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Person>>> GetAll([FromQuery] bool includeInactive = false)
    {
        var query = _db.People.AsQueryable();
        if (!includeInactive)
            query = query.Where(p => p.IsActive);
        return await query.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Person>> Get(int id)
    {
        var person = await _db.FindScopedAsync<Person>(id);
        return person is null ? NotFound() : person;
    }

    [HttpPost]
    public async Task<ActionResult<Person>> Create(Person person)
    {
        _db.People.Add(person);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = person.Id }, person);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Person input)
    {
        if (id != input.Id) return BadRequest();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var person = await _db.FindScopedAsync<Person>(id);
        if (person is null) return NotFound();
        _db.People.Remove(person);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
