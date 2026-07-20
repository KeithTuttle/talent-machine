using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;
using TalentMachine.Api.Services;

namespace TalentMachine.Api.Controllers;

/// <summary>
/// Team management for the current tenant: list members (with their show access),
/// create/revoke join-code invitations (optionally scoped to one show), redeem a
/// code to move into another tenant, grant/revoke show access, remove a member.
///
/// Access model: the Owner (company manager) sees everything and assigns shows;
/// Members (directors, choreographers, music directors, producers…) only see the
/// productions they've been granted. Invites are shared by copying the code.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TeamController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;
    private readonly EmailService _email;

    public TeamController(AppDbContext db, ICurrentTenant tenant, EmailService email)
    {
        _db = db;
        _tenant = tenant;
        _email = email;
    }

    public record MemberDto(
        int Id, string Role, string? Email, string? DisplayName, bool IsYou,
        List<int> ProductionIds);
    public record InvitationDto(
        int Id, string Code, string Name, string Email, string Role, DateTimeOffset CreatedAt,
        int? ProductionId, bool EmailSent, bool CanEmail);
    public record TeamResponse(string TenantName, string YourRole, List<MemberDto> Members, List<InvitationDto> Invitations);
    public record CreateInviteRequest(string Name, string Email, int? ProductionId);
    public record JoinRequest(string Code);
    public record JoinResponse(string TenantName);
    public record AccessRequest(int ProductionId);

    // Unambiguous alphabet (no 0/O/1/I/L) for join codes read out loud or typed.
    private const string CodeAlphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    private string? CallerId =>
        User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    // GET /api/team — members (+ show access) and, for owners, pending invitations.
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
        var access = await _db.ProductionAccesses.ToListAsync(); // tenant-filtered
        var memberDtos = members
            .Select(m => new MemberDto(
                m.Id, m.Role.ToString(), m.Email, m.DisplayName,
                caller != null && m.ClerkUserId == caller,
                access.Where(a => a.MembershipId == m.Id).Select(a => a.ProductionId).ToList()))
            .ToList();

        var invites = new List<InvitationDto>();
        if (_tenant.IsOwner())
        {
            var rows = await _db.Invitations
                .Where(i => i.AcceptedAt == null)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();
            invites = rows
                .Select(i => new InvitationDto(
                    i.Id, i.Code, i.Name, i.Email, i.Role.ToString(), i.CreatedAt,
                    i.ProductionId, i.EmailSentAt != null, _email.IsConfigured))
                .ToList();
        }

        var yourRole = (_tenant.Role ?? MembershipRole.Owner).ToString();
        return new TeamResponse(tenantName, yourRole, memberDtos, invites);
    }

    // POST /api/team/invitations — create a join-code invite (owners only) and
    // email the access instructions automatically. Optionally scoped to one show.
    [HttpPost("invitations")]
    public async Task<ActionResult<InvitationDto>> CreateInvite(CreateInviteRequest input)
    {
        if (!_tenant.IsOwner()) return StatusCode(403);
        if (string.IsNullOrWhiteSpace(input.Name)) return BadRequest("Enter the person's name.");
        if (string.IsNullOrWhiteSpace(input.Email)) return BadRequest("Enter an email address.");

        if (input.ProductionId is not null)
        {
            // Tenant-filtered: an out-of-tenant production simply isn't found.
            var exists = await _db.Productions.AnyAsync(p => p.Id == input.ProductionId);
            if (!exists) return BadRequest("That production doesn't exist.");
        }

        var invite = new Invitation
        {
            Name = input.Name.Trim(),
            Email = input.Email.Trim(),
            Role = MembershipRole.Member,
            ProductionId = input.ProductionId,
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

        // Keep the invitee in the reusable staff directory (autocomplete).
        await EnsureStaffMemberAsync(invite.Name, invite.Email);
        await TrySendInviteAsync(invite);

        return ToDto(invite);
    }

    // POST /api/team/invitations/{id}/send — (re)send the access email (owners only).
    [HttpPost("invitations/{id:int}/send")]
    public async Task<ActionResult<InvitationDto>> ResendInvite(int id)
    {
        if (!_tenant.IsOwner()) return StatusCode(403);
        var invite = await _db.Invitations.FirstOrDefaultAsync(i => i.Id == id && i.AcceptedAt == null);
        if (invite is null) return NotFound();
        if (!_email.IsConfigured)
            return BadRequest("Email isn't set up on the server yet — copy the code instead.");
        var sent = await TrySendInviteAsync(invite);
        if (!sent) return BadRequest("Couldn't send the email — check the server logs, or copy the code instead.");
        return ToDto(invite);
    }

    private async Task<bool> TrySendInviteAsync(Invitation invite)
    {
        if (!_email.IsConfigured || string.IsNullOrWhiteSpace(invite.Email)) return false;
        var tenantName = await _db.Tenants
            .Where(t => t.Id == invite.TenantId).Select(t => t.Name).FirstOrDefaultAsync() ?? "the team";
        var sent = await _email.SendInviteEmailAsync(invite.Email, invite.Name, tenantName, invite.Code);
        if (sent)
        {
            invite.EmailSentAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
        }
        return sent;
    }

    private async Task EnsureStaffMemberAsync(string name, string email)
    {
        var exists = await _db.StaffMembers.AnyAsync(s => s.Email == email);
        if (!exists)
        {
            _db.StaffMembers.Add(new StaffMember { Name = name, Email = email });
            await _db.SaveChangesAsync();
        }
    }

    private InvitationDto ToDto(Invitation i) => new(
        i.Id, i.Code, i.Name, i.Email, i.Role.ToString(), i.CreatedAt,
        i.ProductionId, i.EmailSentAt != null, _email.IsConfigured);

    // DELETE /api/team/invitations/{id} — revoke a pending invite (owners only).
    [HttpDelete("invitations/{id:int}")]
    public async Task<IActionResult> RevokeInvite(int id)
    {
        if (!_tenant.IsOwner()) return StatusCode(403);
        var invite = await _db.Invitations.FirstOrDefaultAsync(i => i.Id == id && i.AcceptedAt == null);
        if (invite is null) return NotFound();
        _db.Invitations.Remove(invite);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/team/join — redeem a code: move the caller's membership into the
    // inviting tenant (as a Member) and grant the invite's show, if any. Their
    // auto-provisioned empty tenant is left behind.
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

        // Show-scoped invite → grant that show. TenantId is set explicitly: the
        // row belongs to the INVITING tenant, not the caller's current one.
        if (invite.ProductionId is not null)
        {
            _db.ProductionAccesses.Add(new ProductionAccess
            {
                TenantId = invite.TenantId,
                MembershipId = membership.Id,
                ProductionId = invite.ProductionId.Value,
            });
        }

        await _db.SaveChangesAsync();

        return new JoinResponse(tenantName);
    }

    // POST /api/team/members/{id}/access — grant a member access to a show (owners only).
    [HttpPost("members/{id:int}/access")]
    public async Task<IActionResult> GrantAccess(int id, AccessRequest input)
    {
        if (!_tenant.IsOwner()) return StatusCode(403);
        var tenantId = _tenant.TenantId ?? 0;

        var membership = await _db.Memberships.FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tenantId);
        if (membership is null) return NotFound();
        var productionExists = await _db.Productions.AnyAsync(p => p.Id == input.ProductionId);
        if (!productionExists) return BadRequest("That production doesn't exist.");

        var existing = await _db.ProductionAccesses.FirstOrDefaultAsync(
            a => a.MembershipId == id && a.ProductionId == input.ProductionId);
        if (existing is null)
        {
            _db.ProductionAccesses.Add(new ProductionAccess { MembershipId = id, ProductionId = input.ProductionId });
            await _db.SaveChangesAsync();
        }
        return NoContent();
    }

    // DELETE /api/team/members/{id}/access/{productionId} — revoke show access (owners only).
    [HttpDelete("members/{id:int}/access/{productionId:int}")]
    public async Task<IActionResult> RevokeAccess(int id, int productionId)
    {
        if (!_tenant.IsOwner()) return StatusCode(403);
        var grant = await _db.ProductionAccesses.FirstOrDefaultAsync(
            a => a.MembershipId == id && a.ProductionId == productionId);
        if (grant is null) return NotFound();
        _db.ProductionAccesses.Remove(grant);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/team/members/{id} — remove a member from the tenant (owners only,
    // not yourself). Their next sign-in auto-provisions a fresh empty tenant.
    [HttpDelete("members/{id:int}")]
    public async Task<IActionResult> RemoveMember(int id)
    {
        if (!_tenant.IsOwner()) return StatusCode(403);
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
