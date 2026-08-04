using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>
/// The companies (tenants) the signed-in user belongs to — powers the company
/// switcher. A user can be an Owner of several and a Member of others; the active
/// one is chosen client-side via the X-Tenant-Id header.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CompaniesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public CompaniesController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public record CompanyDto(int TenantId, string Name, string Role, bool IsActive);
    public record CreateCompanyRequest(string Name);

    private string? CallerId =>
        User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    // GET /api/companies — every company this user is a member of.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CompanyDto>>> GetAll()
    {
        var caller = CallerId;
        if (caller is null) return new List<CompanyDto>(); // dev / anonymous: no companies

        // Memberships + Tenants are global tables (not tenant-filtered).
        var rows = await _db.Memberships
            .Where(m => m.ClerkUserId == caller)
            .Join(_db.Tenants, m => m.TenantId, t => t.Id, (m, t) => new { m, t.Name })
            .OrderBy(x => x.m.CreatedAt).ThenBy(x => x.m.Id)
            .ToListAsync();

        var active = _tenant.TenantId;
        return rows
            .Select(x => new CompanyDto(x.m.TenantId, x.Name, x.m.Role.ToString(), x.m.TenantId == active))
            .ToList();
    }

    // POST /api/companies — create a new company with the caller as its Owner.
    [HttpPost]
    public async Task<ActionResult<CompanyDto>> Create(CreateCompanyRequest input)
    {
        var caller = CallerId;
        if (caller is null) return Unauthorized();
        var name = input.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Enter a company name.");

        var tenant = new Tenant { Name = name };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        var membership = new Membership
        {
            TenantId = tenant.Id,
            ClerkUserId = caller,
            Role = MembershipRole.Owner,
            Email = User.FindFirst("email")?.Value ?? User.FindFirst(ClaimTypes.Email)?.Value,
            DisplayName = User.FindFirst("name")?.Value,
        };
        _db.Memberships.Add(membership);
        await _db.SaveChangesAsync();

        return new CompanyDto(tenant.Id, tenant.Name, membership.Role.ToString(), false);
    }
}
