using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>Per-show creative-team assignments (director, choreographer, …).</summary>
[ApiController]
[Route("api/[controller]")]
public class ProductionStaffController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public ProductionStaffController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public record AssignRequest(int ProductionId, int StaffMemberId, StaffRole Role);

    // GET /api/productionstaff?productionId= (also ?staffMemberId= for the directory)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductionStaff>>> GetAll(
        [FromQuery] int? productionId, [FromQuery] int? staffMemberId)
    {
        var query = _db.ProductionStaff.AsQueryable();
        var accessible = _tenant.AccessibleProductionIds;
        if (accessible is not null)
            query = query.Where(s => accessible.Contains(s.ProductionId));
        if (productionId is not null)
            query = query.Where(s => s.ProductionId == productionId);
        if (staffMemberId is not null)
            query = query.Where(s => s.StaffMemberId == staffMemberId);
        return await query.ToListAsync();
    }

    // POST /api/productionstaff — assign a role (idempotent on the composite key).
    [HttpPost]
    public async Task<ActionResult<ProductionStaff>> Assign(AssignRequest input)
    {
        if (!_tenant.CanAccessProduction(input.ProductionId)) return StatusCode(403);
        var existing = await _db.ProductionStaff.FirstOrDefaultAsync(
            s => s.ProductionId == input.ProductionId && s.StaffMemberId == input.StaffMemberId && s.Role == input.Role);
        if (existing is not null) return Ok(existing);

        var row = new ProductionStaff
        {
            ProductionId = input.ProductionId,
            StaffMemberId = input.StaffMemberId,
            Role = input.Role,
        };
        _db.ProductionStaff.Add(row);
        await _db.SaveChangesAsync();
        return Ok(row);
    }

    // DELETE /api/productionstaff?productionId=&staffMemberId=&role=
    [HttpDelete]
    public async Task<IActionResult> Remove(
        [FromQuery] int productionId, [FromQuery] int staffMemberId, [FromQuery] StaffRole role)
    {
        var row = await _db.ProductionStaff.FirstOrDefaultAsync(
            s => s.ProductionId == productionId && s.StaffMemberId == staffMemberId && s.Role == role);
        if (row is null || !_tenant.CanAccessProduction(row.ProductionId)) return NotFound();
        _db.ProductionStaff.Remove(row);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
