using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;
using TalentMachine.Api.Services;

namespace TalentMachine.Api.Controllers;

/// <summary>
/// Bulk cast import: each row creates-or-reuses a performer, links a (deduped)
/// guardian, and adds the performer to a production's cast in a named group.
/// One access check, one SaveChanges.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CastImportController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public CastImportController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public record CastImportRow(
        int? ExistingPerformerId, string FirstName, string LastName, string? Gender,
        string? DateOfBirth, string? Notes, string? CastGroup,
        string? GuardianName, string? GuardianEmail, string? GuardianPhone);
    public record CastImportRequest(int ProductionId, List<CastImportRow> Rows);
    public record CastImportSummary(
        int PerformersCreated, int PerformersMatched, int AddedToCast,
        int AlreadyInCast, int GuardiansCreated, int GroupsCreated);
    public record AiImportRequest(int ProductionId, List<List<string>> Rows);

    // POST /api/castimport
    [HttpPost]
    public async Task<ActionResult<CastImportSummary>> Import(CastImportRequest input)
    {
        if (!_tenant.CanAccessProduction(input.ProductionId)) return StatusCode(403);

        // Working sets (include rows created during this import so siblings/duplicates dedupe).
        var validPerformerIds = (await _db.Performers.Select(p => p.Id).ToListAsync()).ToHashSet();
        var guardians = await _db.Guardians.ToListAsync();
        var links = (await _db.PerformerGuardians.ToListAsync())
            .Select(l => (l.PerformerId, l.GuardianId)).ToHashSet();
        var groups = await _db.CastGroups.Where(g => g.ProductionId == input.ProductionId).ToListAsync();
        var memberships = await _db.CastMemberships.Where(m => m.ProductionId == input.ProductionId).ToListAsync();
        var castPerformerIds = memberships.Select(m => m.PerformerId).ToHashSet();

        int performersCreated = 0, performersMatched = 0, addedToCast = 0,
            alreadyInCast = 0, guardiansCreated = 0, groupsCreated = 0;

        foreach (var row in input.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.FirstName) && string.IsNullOrWhiteSpace(row.LastName)) continue;

            // --- Performer: reuse an existing one, or create. ---
            Performer? newPerformer = null; // set only when we created one this row
            int existingId = 0;
            if (row.ExistingPerformerId is int pid && validPerformerIds.Contains(pid))
            {
                existingId = pid;
                performersMatched++;
            }
            else
            {
                newPerformer = new Performer
                {
                    FirstName = row.FirstName.Trim(),
                    LastName = row.LastName.Trim(),
                    Gender = Enum.TryParse<Gender>(row.Gender, ignoreCase: true, out var g) ? g : null,
                    DateOfBirth = DateOnly.TryParse(row.DateOfBirth, out var dob) ? dob : null,
                    Notes = string.IsNullOrWhiteSpace(row.Notes) ? null : row.Notes.Trim(),
                };
                _db.Performers.Add(newPerformer);
                performersCreated++;
            }

            // --- Guardian: dedupe by email (else name), link if not already linked. ---
            var gname = row.GuardianName?.Trim();
            var gemail = row.GuardianEmail?.Trim();
            if (!string.IsNullOrWhiteSpace(gname) || !string.IsNullOrWhiteSpace(gemail))
            {
                var guardian = FindGuardian(guardians, gemail, gname);
                if (guardian is null)
                {
                    guardian = new Guardian
                    {
                        Name = !string.IsNullOrWhiteSpace(gname) ? gname : gemail!,
                        Email = string.IsNullOrWhiteSpace(gemail) ? null : gemail,
                        Phone = string.IsNullOrWhiteSpace(row.GuardianPhone) ? null : row.GuardianPhone!.Trim(),
                    };
                    _db.Guardians.Add(guardian);
                    guardians.Add(guardian);
                    guardiansCreated++;
                }
                // Link (skip if this exact pair already exists — only checkable for existing performers).
                if (existingId == 0 || guardian.Id == 0 || !links.Contains((existingId, guardian.Id)))
                {
                    var link = new PerformerGuardian();
                    if (newPerformer is not null) link.Performer = newPerformer; else link.PerformerId = existingId;
                    if (guardian.Id != 0) link.GuardianId = guardian.Id; else link.Guardian = guardian;
                    _db.PerformerGuardians.Add(link);
                    if (existingId != 0 && guardian.Id != 0) links.Add((existingId, guardian.Id));
                }
            }

            // --- Cast group: find-or-create by name in the production. ---
            CastGroup? groupEntity = null;
            int? groupId = null;
            if (!string.IsNullOrWhiteSpace(row.CastGroup))
            {
                var name = row.CastGroup.Trim();
                var grp = groups.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                if (grp is null)
                {
                    grp = new CastGroup { ProductionId = input.ProductionId, Name = name, OrderIndex = groups.Count + 1 };
                    _db.CastGroups.Add(grp);
                    groups.Add(grp);
                    groupsCreated++;
                }
                if (grp.Id != 0) groupId = grp.Id; else groupEntity = grp;
            }

            // --- Cast membership: add to the show, or update the existing one's group. ---
            if (existingId != 0 && castPerformerIds.Contains(existingId))
            {
                alreadyInCast++;
                var m = memberships.First(x => x.PerformerId == existingId);
                if (groupEntity is not null) m.CastGroup = groupEntity;
                else if (groupId is not null) m.CastGroupId = groupId;
            }
            else
            {
                var membership = new CastMembership { ProductionId = input.ProductionId };
                if (newPerformer is not null) membership.Performer = newPerformer; else membership.PerformerId = existingId;
                if (groupEntity is not null) membership.CastGroup = groupEntity;
                else membership.CastGroupId = groupId;
                _db.CastMemberships.Add(membership);
                memberships.Add(membership);
                addedToCast++;
                if (existingId != 0) castPerformerIds.Add(existingId);
            }
        }

        await _db.SaveChangesAsync();
        return new CastImportSummary(
            performersCreated, performersMatched, addedToCast, alreadyInCast, guardiansCreated, groupsCreated);
    }

    // POST /api/castimport/ai — extract a messy roster into structured rows to review.
    [HttpPost("ai")]
    public async Task<ActionResult<AiCastResult>> ImportAi(
        AiImportRequest input, [FromServices] CastImportAiService ai)
    {
        if (!_tenant.CanAccessProduction(input.ProductionId)) return StatusCode(403);
        if (!ai.IsConfigured) return new AiCastResult(false, false, new());
        return await ai.ExtractAsync(input.Rows);
    }

    private static Guardian? FindGuardian(List<Guardian> guardians, string? email, string? name)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            var byEmail = guardians.FirstOrDefault(g =>
                !string.IsNullOrWhiteSpace(g.Email) && string.Equals(g.Email, email, StringComparison.OrdinalIgnoreCase));
            if (byEmail is not null) return byEmail;
        }
        if (!string.IsNullOrWhiteSpace(name))
            return guardians.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
        return null;
    }
}
