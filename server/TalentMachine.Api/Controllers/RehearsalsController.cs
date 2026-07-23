using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Models;
using TalentMachine.Api.Services;

namespace TalentMachine.Api.Controllers;

/// <summary>
/// Rehearsal slots plus the schedule PDF and the AI suggestion endpoint.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RehearsalsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public RehearsalsController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public record BulkRequest(int ProductionId, List<Rehearsal> Rehearsals);
    public record SuggestRequest(int ProductionId, string? Prompt, DateOnly FromDate, DateOnly ToDate);
    public record SuggestResponse(bool Configured, bool Ok, List<SuggestedSlot> Slots);
    // Subject/Body are optional overrides: when the user edits the preview, send
    // exactly what they typed (recipients + PDF are still resolved server-side).
    public record EmailRequest(int ProductionId, DateOnly From, DateOnly To, string Audience,
        string? Subject = null, string? Body = null);
    public record EmailPreview(bool Configured, string Subject, string Body, List<string> Recipients, List<string> MissingEmail);
    public record EmailResult(bool Sent, int Count);

    // GET /api/rehearsals?productionId=&from=&to=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Rehearsal>>> GetAll(
        [FromQuery] int? productionId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var query = _db.Rehearsals.AsQueryable();
        var accessible = _tenant.AccessibleProductionIds;
        if (accessible is not null)
            query = query.Where(r => accessible.Contains(r.ProductionId));
        if (productionId is not null)
            query = query.Where(r => r.ProductionId == productionId);
        if (from is not null)
            query = query.Where(r => r.Date >= from);
        if (to is not null)
            query = query.Where(r => r.Date <= to);
        return await query.OrderBy(r => r.Date).ThenBy(r => r.StartTime).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Rehearsal>> Get(int id)
    {
        var rehearsal = await _db.FindScopedAsync<Rehearsal>(id);
        if (rehearsal is null || !_tenant.CanAccessProduction(rehearsal.ProductionId)) return NotFound();
        return rehearsal;
    }

    [HttpPost]
    public async Task<ActionResult<Rehearsal>> Create(Rehearsal rehearsal)
    {
        if (!_tenant.CanAccessProduction(rehearsal.ProductionId)) return StatusCode(403);
        _db.Rehearsals.Add(rehearsal);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = rehearsal.Id }, rehearsal);
    }

    // POST /api/rehearsals/bulk — save an AI-suggested draft in one call.
    [HttpPost("bulk")]
    public async Task<ActionResult<IEnumerable<Rehearsal>>> CreateBulk(BulkRequest input)
    {
        if (!_tenant.CanAccessProduction(input.ProductionId)) return StatusCode(403);
        foreach (var r in input.Rehearsals)
        {
            r.Id = 0;
            r.ProductionId = input.ProductionId;
        }
        _db.Rehearsals.AddRange(input.Rehearsals);
        await _db.SaveChangesAsync();
        return Ok(input.Rehearsals);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Rehearsal input)
    {
        if (id != input.Id) return BadRequest();
        if (!_tenant.CanAccessProduction(input.ProductionId)) return NotFound();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var rehearsal = await _db.FindScopedAsync<Rehearsal>(id);
        if (rehearsal is null || !_tenant.CanAccessProduction(rehearsal.ProductionId)) return NotFound();
        _db.Rehearsals.Remove(rehearsal);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/rehearsals/pdf?productionId=&from=&to= — printable weekly schedule.
    [HttpGet("pdf")]
    public async Task<IActionResult> Pdf(
        [FromQuery] int productionId, [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromServices] RehearsalPdfService pdf)
    {
        if (!_tenant.CanAccessProduction(productionId)) return NotFound();
        var production = await _db.FindScopedAsync<Production>(productionId);
        if (production is null) return NotFound();

        var slots = await _db.Rehearsals
            .Where(r => r.ProductionId == productionId && r.Date >= from && r.Date <= to)
            .ToListAsync();
        var slotIds = slots.Select(s => s.Id).ToList();
        var numberIds = await _db.Numbers
            .Where(n => n.ProductionId == productionId).Select(n => n.Id).ToListAsync();

        var data = new RehearsalPdfData
        {
            ProductionTitle = production.Title,
            From = from,
            To = to,
            Slots = slots,
            Overrides = await _db.RehearsalAttendees.Where(a => slotIds.Contains(a.RehearsalId)).ToListAsync(),
            NumberCasts = await _db.NumberCasts.Where(c => numberIds.Contains(c.MusicalNumberId)).ToListAsync(),
            Numbers = await _db.Numbers.Where(n => n.ProductionId == productionId).ToListAsync(),
            Performers = await _db.Performers.ToListAsync(),
            Conflicts = await _db.Conflicts.Where(c => c.ProductionId == productionId).ToListAsync(),
        };

        var bytes = pdf.Build(data);
        return File(bytes, "application/pdf", $"rehearsals-{from:yyyy-MM-dd}.pdf");
    }

    // POST /api/rehearsals/suggest — AI schedule draft. Returns configured:false
    // when Gemini isn't set up (the ONLY case the client hides the feature).
    [HttpPost("suggest")]
    public async Task<ActionResult<SuggestResponse>> Suggest(
        SuggestRequest input, [FromServices] RehearsalAiService ai)
    {
        if (!_tenant.CanAccessProduction(input.ProductionId)) return StatusCode(403);
        if (!ai.IsConfigured) return new SuggestResponse(false, false, new());

        var production = await _db.FindScopedAsync<Production>(input.ProductionId);
        if (production is null) return NotFound();

        var numbers = await _db.Numbers
            .Where(n => n.ProductionId == input.ProductionId)
            .OrderBy(n => n.OrderIndex).ToListAsync();
        var numberIds = numbers.Select(n => n.Id).ToList();
        var numberCasts = await _db.NumberCasts
            .Where(c => numberIds.Contains(c.MusicalNumberId)).ToListAsync();
        var castIds = await _db.CastMemberships
            .Where(m => m.ProductionId == input.ProductionId).Select(m => m.PerformerId).ToListAsync();
        var cast = await _db.Performers.Where(p => castIds.Contains(p.Id)).ToListAsync();
        // Conflicts possibly active in the range: one-offs overlapping it + all weeklies.
        var conflicts = (await _db.Conflicts
                .Where(c => c.ProductionId == input.ProductionId).ToListAsync())
            .Where(c => c.Type == ConflictType.Weekly
                || ((c.EndDate ?? c.StartDate) >= input.FromDate && c.StartDate <= input.ToDate))
            .ToList();

        var result = await ai.SuggestAsync(new SuggestContext(
            production.Title, input.FromDate, input.ToDate, input.Prompt,
            numbers, numberCasts.ToLookup(c => c.MusicalNumberId, c => c.PerformerId),
            cast, conflicts));

        return new SuggestResponse(result.Configured, result.Ok, result.Slots);
    }

    // POST /api/rehearsals/email/preview — the exact email that WILL be sent
    // (subject, body, recipient list), without sending anything.
    [HttpPost("email/preview")]
    public async Task<ActionResult<EmailPreview>> EmailPreviewAction(
        EmailRequest input, [FromServices] EmailService email)
    {
        if (!_tenant.CanAccessProduction(input.ProductionId)) return StatusCode(403);
        var built = await BuildEmailAsync(input);
        if (built is null) return NotFound();
        return new EmailPreview(email.IsConfigured, built.Value.Subject, built.Value.Body,
            built.Value.Recipients, built.Value.Missing);
    }

    // POST /api/rehearsals/email/send — send the schedule (PDF attached) to the
    // chosen guardians. Never auto-sends; the client calls this on an explicit click.
    [HttpPost("email/send")]
    public async Task<ActionResult<EmailResult>> EmailSend(
        EmailRequest input, [FromServices] EmailService email, [FromServices] RehearsalPdfService pdf)
    {
        if (!_tenant.CanAccessProduction(input.ProductionId)) return StatusCode(403);
        if (!email.IsConfigured) return new EmailResult(false, 0);
        var built = await BuildEmailAsync(input);
        if (built is null) return NotFound();
        if (built.Value.Recipients.Count == 0) return new EmailResult(false, 0);

        var bytes = pdf.Build(built.Value.PdfData);
        // Honor edits from the preview; fall back to the composed defaults.
        var subject = string.IsNullOrWhiteSpace(input.Subject) ? built.Value.Subject : input.Subject!;
        var body = string.IsNullOrWhiteSpace(input.Body) ? built.Value.Body : input.Body!;
        var sent = await email.SendAsync(built.Value.Recipients, subject, body,
            bytes, $"rehearsals-{input.From:yyyy-MM-dd}.pdf");
        return new EmailResult(sent, sent ? built.Value.Recipients.Count : 0);
    }

    private readonly record struct BuiltEmail(
        string Subject, string Body, List<string> Recipients, List<string> Missing, RehearsalPdfData PdfData);

    /// <summary>Resolves recipients + composes the body once, shared by preview and send.</summary>
    private async Task<BuiltEmail?> BuildEmailAsync(EmailRequest input)
    {
        var production = await _db.FindScopedAsync<Production>(input.ProductionId);
        if (production is null) return null;

        var slots = await _db.Rehearsals
            .Where(r => r.ProductionId == input.ProductionId && r.Date >= input.From && r.Date <= input.To)
            .OrderBy(r => r.Date).ThenBy(r => r.StartTime).ToListAsync();
        var slotIds = slots.Select(s => s.Id).ToList();
        var numbers = await _db.Numbers.Where(n => n.ProductionId == input.ProductionId).ToListAsync();
        var numberIds = numbers.Select(n => n.Id).ToList();
        var numberCasts = await _db.NumberCasts.Where(c => numberIds.Contains(c.MusicalNumberId)).ToListAsync();
        var overrides = await _db.RehearsalAttendees.Where(a => slotIds.Contains(a.RehearsalId)).ToListAsync();
        var castByNumber = numberCasts.ToLookup(c => c.MusicalNumberId, c => c.PerformerId);

        // Audience performers: whole cast, or only kids scheduled this week.
        HashSet<int> audienceIds;
        if (input.Audience == "scheduled")
        {
            audienceIds = new HashSet<int>();
            foreach (var slot in slots)
                foreach (var pid in RehearsalResolver.ResolveAttendees(slot, castByNumber, overrides))
                    audienceIds.Add(pid);
        }
        else
        {
            audienceIds = (await _db.CastMemberships
                .Where(m => m.ProductionId == input.ProductionId).Select(m => m.PerformerId).ToListAsync())
                .ToHashSet();
        }

        var performers = await _db.Performers.Where(p => audienceIds.Contains(p.Id)).ToListAsync();
        var links = await _db.PerformerGuardians.Where(l => audienceIds.Contains(l.PerformerId)).ToListAsync();
        var guardians = await _db.Guardians.ToListAsync();
        var guardianById = guardians.ToDictionary(g => g.Id);

        var recipients = links
            .Select(l => guardianById.GetValueOrDefault(l.GuardianId)?.Email)
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e!.Trim())
            .Distinct()
            .ToList();

        var withEmail = new HashSet<int>(links
            .Where(l => !string.IsNullOrWhiteSpace(guardianById.GetValueOrDefault(l.GuardianId)?.Email))
            .Select(l => l.PerformerId));
        var missing = performers.Where(p => !withEmail.Contains(p.Id))
            .Select(p => $"{p.FirstName} {p.LastName}".Trim()).OrderBy(n => n).ToList();

        var allPerformers = await _db.Performers.ToListAsync();
        var nameById = allPerformers.ToDictionary(p => p.Id, p => $"{p.FirstName} {p.LastName}".Trim());
        var numberTitle = numbers.ToDictionary(n => n.Id, n => n.Title);
        var subject = $"Rehearsal schedule: {production.Title} — {input.From:MMM d}–{input.To:MMM d, yyyy}";
        var body = ComposeBody(production.Title, input.From, input.To, slots, numberTitle, castByNumber, overrides, nameById);

        var pdfData = new RehearsalPdfData
        {
            ProductionTitle = production.Title,
            From = input.From,
            To = input.To,
            Slots = slots,
            Overrides = overrides,
            NumberCasts = numberCasts,
            Numbers = numbers,
            Performers = allPerformers,
            Conflicts = await _db.Conflicts.Where(c => c.ProductionId == input.ProductionId).ToListAsync(),
        };

        return new BuiltEmail(subject, body, recipients, missing, pdfData);
    }

    private static string ComposeBody(
        string title, DateOnly from, DateOnly to, List<Rehearsal> slots,
        Dictionary<int, string> numberTitle, ILookup<int, int> castByNumber,
        List<RehearsalAttendee> overrides, Dictionary<int, string> nameById)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("REHEARSAL SCHEDULE");
        sb.AppendLine(title);
        sb.AppendLine($"{from:MMM d} – {to:MMM d, yyyy}");
        sb.AppendLine();
        sb.AppendLine("Please check the call times below. Under each rehearsal are the");
        sb.AppendLine("performers needed for it — if your performer isn't listed, they're");
        sb.AppendLine("not called for that slot.");
        sb.AppendLine();

        if (slots.Count == 0)
        {
            sb.AppendLine("No rehearsals are scheduled for this week.");
        }
        else
        {
            DateOnly? day = null;
            foreach (var s in slots)
            {
                if (s.Date != day)
                {
                    day = s.Date;
                    sb.AppendLine(s.Date.ToString("dddd, MMMM d").ToUpperInvariant());
                    sb.AppendLine(new string('-', 34));
                }

                var name = s.MusicalNumberId is int nid && numberTitle.TryGetValue(nid, out var t)
                    ? t : "General / all-company";
                sb.AppendLine($"  {s.StartTime:h:mm tt} – {s.EndTime:h:mm tt}   {name}  ({s.Type})");
                if (!string.IsNullOrWhiteSpace(s.Room)) sb.AppendLine($"      Room: {s.Room}");
                if (!string.IsNullOrWhiteSpace(s.Notes)) sb.AppendLine($"      Note: {s.Notes}");

                var needed = RehearsalResolver.ResolveAttendees(s, castByNumber, overrides)
                    .Select(id => nameById.GetValueOrDefault(id, $"#{id}"))
                    .OrderBy(n => n)
                    .ToList();
                sb.AppendLine(needed.Count == 0
                    ? "      Needed: (no cast assigned yet)"
                    : $"      Needed ({needed.Count}): {string.Join(", ", needed)}");
                sb.AppendLine();
            }
        }

        sb.AppendLine("The full schedule is also attached as a PDF.");
        sb.AppendLine();
        sb.AppendLine("See you there!");
        sb.AppendLine("— The Talent Machine Company");
        return sb.ToString();
    }
}
