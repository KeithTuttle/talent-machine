using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>Which characters (Roles) are featured in which musical numbers.</summary>
[ApiController]
[Route("api/[controller]")]
public class NumberCharactersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public NumberCharactersController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public record TagRequest(int MusicalNumberId, int RoleId);

    // GET /api/numbercharacters?numberId=       (one number's characters)
    // GET /api/numbercharacters?productionId=   (all tags in a production)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NumberCharacter>>> GetAll(
        [FromQuery] int? numberId, [FromQuery] int? productionId)
    {
        var query = _db.NumberCharacters.AsQueryable();
        var accessible = _tenant.AccessibleProductionIds;
        if (accessible is not null)
            query = query.Where(nc => nc.MusicalNumber != null && accessible.Contains(nc.MusicalNumber.ProductionId));
        if (numberId is not null)
            query = query.Where(nc => nc.MusicalNumberId == numberId);
        if (productionId is not null)
            query = query.Where(nc => nc.MusicalNumber != null && nc.MusicalNumber.ProductionId == productionId);
        return await query.ToListAsync();
    }

    // POST /api/numbercharacters — tag a character into a number (idempotent).
    [HttpPost]
    public async Task<ActionResult<NumberCharacter>> Add(TagRequest input)
    {
        var number = await _db.Numbers.FirstOrDefaultAsync(n => n.Id == input.MusicalNumberId);
        if (number is null || !_tenant.CanAccessProduction(number.ProductionId)) return NotFound();

        var existing = await _db.NumberCharacters.FirstOrDefaultAsync(
            nc => nc.MusicalNumberId == input.MusicalNumberId && nc.RoleId == input.RoleId);
        if (existing is not null) return Ok(existing);

        var row = new NumberCharacter { MusicalNumberId = input.MusicalNumberId, RoleId = input.RoleId };
        _db.NumberCharacters.Add(row);
        await _db.SaveChangesAsync();
        return Ok(row);
    }

    // DELETE /api/numbercharacters?numberId=&roleId=
    [HttpDelete]
    public async Task<IActionResult> Remove([FromQuery] int numberId, [FromQuery] int roleId)
    {
        var row = await _db.NumberCharacters
            .Include(nc => nc.MusicalNumber)
            .FirstOrDefaultAsync(nc => nc.MusicalNumberId == numberId && nc.RoleId == roleId);
        if (row is null || row.MusicalNumber is null
            || !_tenant.CanAccessProduction(row.MusicalNumber.ProductionId)) return NotFound();

        _db.NumberCharacters.Remove(row);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
