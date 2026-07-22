using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>Which characters (Roles) are present in which scenes.</summary>
[ApiController]
[Route("api/[controller]")]
public class SceneCharactersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public SceneCharactersController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public record PresenceRequest(int SceneId, int RoleId);

    // GET /api/scenecharacters?sceneId=       (one scene's characters)
    // GET /api/scenecharacters?productionId=  (all presence in a production)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SceneCharacter>>> GetAll(
        [FromQuery] int? sceneId, [FromQuery] int? productionId)
    {
        var query = _db.SceneCharacters.AsQueryable();
        var accessible = _tenant.AccessibleProductionIds;
        if (accessible is not null)
            query = query.Where(sc => sc.Scene != null && accessible.Contains(sc.Scene.ProductionId));
        if (sceneId is not null)
            query = query.Where(sc => sc.SceneId == sceneId);
        if (productionId is not null)
            query = query.Where(sc => sc.Scene != null && sc.Scene.ProductionId == productionId);
        return await query.ToListAsync();
    }

    // POST /api/scenecharacters — mark a character present in a scene (idempotent).
    [HttpPost]
    public async Task<ActionResult<SceneCharacter>> Add(PresenceRequest input)
    {
        var scene = await _db.Scenes.FirstOrDefaultAsync(s => s.Id == input.SceneId);
        if (scene is null || !_tenant.CanAccessProduction(scene.ProductionId)) return NotFound();

        var existing = await _db.SceneCharacters.FirstOrDefaultAsync(
            sc => sc.SceneId == input.SceneId && sc.RoleId == input.RoleId);
        if (existing is not null) return Ok(existing);

        var row = new SceneCharacter { SceneId = input.SceneId, RoleId = input.RoleId };
        _db.SceneCharacters.Add(row);
        await _db.SaveChangesAsync();
        return Ok(row);
    }

    // DELETE /api/scenecharacters?sceneId=&roleId=
    [HttpDelete]
    public async Task<IActionResult> Remove([FromQuery] int sceneId, [FromQuery] int roleId)
    {
        var row = await _db.SceneCharacters
            .Include(sc => sc.Scene)
            .FirstOrDefaultAsync(sc => sc.SceneId == sceneId && sc.RoleId == roleId);
        if (row is null || row.Scene is null
            || !_tenant.CanAccessProduction(row.Scene.ProductionId)) return NotFound();

        _db.SceneCharacters.Remove(row);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
