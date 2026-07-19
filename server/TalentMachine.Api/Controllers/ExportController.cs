using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;

namespace TalentMachine.Api.Controllers;

/// <summary>
/// Full-tenant data export: one JSON file with every table the current tenant
/// owns, as a portable backup that isn't the database. No navigation includes —
/// rows reference each other by id, which keeps the file flat and cycle-free.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ExportController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }, // "Owner", not 0
    };

    public ExportController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    // GET /api/export — download everything in the current tenant as JSON.
    // Owner-only: show-level members shouldn't walk off with the whole archive.
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        if (!_tenant.IsOwner()) return StatusCode(403);
        var tenantId = _tenant.TenantId ?? 0;
        var tenantName = await _db.Tenants
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync();

        // Every tenant-scoped DbSet is already filtered to the caller's tenant.
        var export = new
        {
            exportedAt = DateTimeOffset.UtcNow,
            tenantName,
            seasons = await _db.Seasons.AsNoTracking().ToListAsync(),
            productions = await _db.Productions.AsNoTracking().ToListAsync(),
            performers = await _db.Performers.AsNoTracking().ToListAsync(),
            productionAccesses = await _db.ProductionAccesses.AsNoTracking().ToListAsync(),
            castGroups = await _db.CastGroups.AsNoTracking().ToListAsync(),
            levelGroups = await _db.LevelGroups.AsNoTracking().ToListAsync(),
            castMemberships = await _db.CastMemberships.AsNoTracking().ToListAsync(),
            roles = await _db.Roles.AsNoTracking().ToListAsync(),
            numbers = await _db.Numbers.AsNoTracking().ToListAsync(),
            numberCasts = await _db.NumberCasts.AsNoTracking().ToListAsync(),
            conflicts = await _db.Conflicts.AsNoTracking().ToListAsync(),
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(export, JsonOpts);
        var fileName = $"talentmachine-export-{DateTime.UtcNow:yyyy-MM-dd}.json";
        return File(bytes, "application/json", fileName);
    }
}
