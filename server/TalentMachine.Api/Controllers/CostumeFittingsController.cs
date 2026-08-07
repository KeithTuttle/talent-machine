using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>
/// A performer's fit in a costume — size, alteration notes, fitted. One row per
/// (costume, performer), so fitting a kid once counts everywhere that costume is worn.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CostumeFittingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public CostumeFittingsController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public record FittingRequest(int CostumeId, int PerformerId, string? Size, string? Notes, bool IsFitted);

    // GET /api/costumefittings?productionId=|?costumeId=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CostumeFitting>>> GetAll(
        [FromQuery] int? productionId, [FromQuery] int? costumeId)
    {
        var query = _db.CostumeFittings.AsQueryable();
        var accessible = _tenant.AccessibleProductionIds;
        if (accessible is not null)
            query = query.Where(f => f.Costume != null && accessible.Contains(f.Costume.ProductionId));
        if (productionId is not null)
            query = query.Where(f => f.Costume != null && f.Costume.ProductionId == productionId);
        if (costumeId is not null)
            query = query.Where(f => f.CostumeId == costumeId);
        return await query.ToListAsync();
    }

    // POST /api/costumefittings — upsert one performer's fit in one costume.
    [HttpPost]
    public async Task<ActionResult<CostumeFitting>> Upsert(FittingRequest input)
    {
        var costume = await _db.FindScopedAsync<Costume>(input.CostumeId);
        if (costume is null || !_tenant.CanAccessProduction(costume.ProductionId)) return NotFound();

        var existing = await _db.CostumeFittings.FirstOrDefaultAsync(
            f => f.CostumeId == input.CostumeId && f.PerformerId == input.PerformerId);
        if (existing is not null)
        {
            existing.Size = input.Size;
            existing.Notes = input.Notes;
            existing.IsFitted = input.IsFitted;
            await _db.SaveChangesAsync();
            return Ok(existing);
        }

        var row = new CostumeFitting
        {
            CostumeId = input.CostumeId,
            PerformerId = input.PerformerId,
            Size = input.Size,
            Notes = input.Notes,
            IsFitted = input.IsFitted,
        };
        _db.CostumeFittings.Add(row);
        await _db.SaveChangesAsync();
        return Ok(row);
    }
}
