using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Controllers;

/// <summary>
/// Team management for the current tenant: list members, create/revoke join-code
/// invitations, redeem a code to move into another tenant, remove a member.
/// Invites are shared by copying the code (no email sending in this app yet).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TeamController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public TeamController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public record MemberDto(int Id, string Role, string? Email, string? DisplayName, bool IsYou);
    public record InvitationDto(int Id, string Code, string? Email, string Role, DateTimeOffset CreatedAt);
    public record TeamResponse(string TenantName, string YourRole, List<MemberDto> Members, List<InvitationDto> Invitations);
    public record CreateInviteRequest(string? Email);
    public record JoinRequest(string Code);
    public record JoinResponse(string TenantName);

    // Unambiguous alphabet (no 0/O/1/I/L) for join codes read out loud or typed.
    private const string CodeAlphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    private string? CallerId =>
        User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    /// <summary>Owner when resolved; also true when auth is disabled in local dev
    /// (the middleware never runs, so Role stays null — the whole API is open then).</summary>
    private bool IsOwner => _tenant.Role is null or MembershipRole.Owner;

    // GET /api/team — members + (for owners) pending invitations.
    [HttpGet]
    public async Task<ActionResult<TeamResponse>> Get()
    {
        var tenantId = _tenant.TenantId ?? 0;
        var tenantName = await _db.Tenants
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync() ?? "My account";

        var caller = CallerId;
        var members = await _db.Memberships
            .Where(m => m.TenantId == tenantId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
        var memberDtos = members
            .Select(m => new MemberDto(
                m.Id, m.Role.ToString(), m.Email, m.DisplayName, caller != null && m.ClerkUserId == caller))
            .ToList();

        var invites = new List<InvitationDto>();
        if (IsOwner)
        {
            var rows = await _db.Invitations
                .Where(i => i.AcceptedAt == null)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();
            invites = rows
                .Select(i => new InvitationDto(i.Id, i.Code, i.Email, i.Role.ToString(), i.CreatedAt))
                .ToList();
        }

        var yourRole = (_tenant.Role ?? MembershipRole.Owner).ToString();
        return new TeamResponse(tenantName, yourRole, memberDtos, invites);
    }

    // POST /api/team/invitations — create a join-code invite (owners only).
    [HttpPost("invitations")]
    public async Task<ActionResult<InvitationDto>> CreateInvite(CreateInviteRequest input)
    {
        if (!IsOwner) return StatusCode(403);

        var invite = new Invitation
        {
            Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim(),
            Role = MembershipRole.Member,
        };

        // Retry on the (astronomically unlikely) code collision.
        for (var attempt = 0; ; attempt++)
        {
            invite.Code = GenerateCode(8);
            _db.Invitations.Add(invite);
            try
            {
                await _db.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                _db.Entry(invite).State = EntityState.Detached;
            }
        }

        return new InvitationDto(invite.Id, invite.Code, invite.Email, invite.Role.ToString(), invite.CreatedAt);
    }

    // DELETE /api/team/invitations/{id} — revoke a pending invite (owners only).
    [HttpDelete("invitations/{id:int}")]
    public async Task<IActionResult> RevokeInvite(int id)
    {
        if (!IsOwner) return StatusCode(403);
        var invite = await _db.Invitations.FirstOrDefaultAsync(i => i.Id == id && i.AcceptedAt == null);
        if (invite is null) return NotFound();
        _db.Invitations.Remove(invite);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/team/join — redeem a code: move the caller's membership into the
    // inviting tenant. Their auto-provisioned empty tenant is left behind.
    [HttpPost("join")]
    public async Task<ActionResult<JoinResponse>> Join(JoinRequest input)
    {
        var caller = CallerId;
        if (caller is null)
            return BadRequest("Sign in to join a team.");

        var code = input.Code?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code)) return BadRequest("Enter an invite code.");

        // Cross-tenant by design: the invite lives in the *inviting* tenant.
        var invite = await _db.Invitations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Code == code && i.AcceptedAt == null);
        if (invite is null) return NotFound("That invite code isn't valid (it may have been revoked or already used).");

        var membership = await _db.Memberships.FirstOrDefaultAsync(m => m.ClerkUserId == caller);
        if (membership is null) return BadRequest("No account found — reload and try again.");

        var tenantName = await _db.Tenants
            .Where(t => t.Id == invite.TenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync() ?? "the team";

        if (membership.TenantId == invite.TenantId)
            return new JoinResponse(tenantName); // already a member — idempotent

        membership.TenantId = invite.TenantId;
        membership.Role = invite.Role;
        invite.AcceptedAt = DateTimeOffset.UtcNow;
        invite.AcceptedByClerkUserId = caller;
        await _db.SaveChangesAsync();

        return new JoinResponse(tenantName);
    }

    // DELETE /api/team/members/{id} — remove a member from the tenant (owners only,
    // not yourself). Their next sign-in auto-provisions a fresh empty tenant.
    [HttpDelete("members/{id:int}")]
    public async Task<IActionResult> RemoveMember(int id)
    {
        if (!IsOwner) return StatusCode(403);
        var tenantId = _tenant.TenantId ?? 0;

        var membership = await _db.Memberships.FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tenantId);
        if (membership is null) return NotFound();
        if (membership.ClerkUserId == CallerId)
            return BadRequest("You can't remove yourself.");

        _db.Memberships.Remove(membership);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static string GenerateCode(int length)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        return new string(chars);
    }
}
