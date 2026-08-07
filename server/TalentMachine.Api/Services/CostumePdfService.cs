using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Services;

/// <summary>Data for the backstage costume sheet.</summary>
public class CostumeSheetData
{
    public string ProductionTitle { get; set; } = string.Empty;
    public List<Act> Acts { get; set; } = new();
    public List<MusicalNumber> Numbers { get; set; } = new();
    public List<Costume> Costumes { get; set; } = new();
    public List<CostumeNumber> CostumeNumbers { get; set; } = new();
    public List<CostumeAssignment> Assignments { get; set; } = new();
    public List<CostumeFitting> Fittings { get; set; } = new();
    public List<NumberCast> NumberCasts { get; set; } = new();
    public List<Performer> Performers { get; set; } = new();
}

/// <summary>
/// A high-contrast, backstage-readable costume sheet: every number in running
/// order with its costume label, the costumes it wears (with accessories/shoes
/// and vendor/photo links), and the kids in each — with sizes and alteration
/// notes, and on-stage extras (not in the number) flagged.
/// </summary>
public class CostumePdfService
{
    public byte[] Build(CostumeSheetData data)
    {
        var performerName = data.Performers.ToDictionary(p => p.Id, p => $"{p.FirstName} {p.LastName}".Trim());
        var performerGender = data.Performers.ToDictionary(p => p.Id, p => p.Gender);
        var castByNumber = data.NumberCasts.ToLookup(c => c.MusicalNumberId, c => c.PerformerId);
        var assignmentsByNumber = data.Assignments.ToLookup(a => a.MusicalNumberId);
        var fittingByKey = data.Fittings
            .GroupBy(f => (f.CostumeId, f.PerformerId))
            .ToDictionary(g => g.Key, g => g.First());
        var costumeById = data.Costumes.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
        // Costumes a number wears: its explicit links, plus any a kid is assigned
        // (so an assignment can never be orphaned off the sheet).
        var costumesByNumber = data.CostumeNumbers
            .Select(cn => (cn.MusicalNumberId, cn.CostumeId))
            .Concat(data.Assignments.Where(a => a.CostumeId != null)
                .Select(a => (a.MusicalNumberId, CostumeId: a.CostumeId!.Value)))
            .Distinct()
            .Where(x => costumeById.ContainsKey(x.CostumeId))
            .ToLookup(x => x.MusicalNumberId, x => costumeById[x.CostumeId]);

        var ordered = CostumeChanges.RunningOrder(data.Acts, data.Numbers);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken4));

                page.Header().Column(col =>
                {
                    col.Item().Text("Costume Sheet").FontSize(20).SemiBold().FontColor(Colors.Black);
                    col.Item().Text(data.ProductionTitle).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(12);
                    if (ordered.Count == 0)
                    {
                        col.Item().Text("No numbers yet.").Italic().FontColor(Colors.Grey.Medium);
                        return;
                    }
                    for (var i = 0; i < ordered.Count; i++)
                    {
                        var n = ordered[i];
                        col.Item().Element(c => ComposeNumber(
                            c, i + 1, n, costumesByNumber[n.Id].OrderBy(x => x.Name).ToList(),
                            assignmentsByNumber[n.Id].ToList(), fittingByKey,
                            castByNumber[n.Id].ToHashSet(), performerName, performerGender));
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Costume sheet — generated ");
                    t.Span(DateTime.Now.ToString("MMM d, yyyy"));
                    t.Span("   •   Page ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private void ComposeNumber(
        IContainer container, int runningNumber, MusicalNumber number,
        List<Costume> costumes, List<CostumeAssignment> assignments,
        Dictionary<(int, int), CostumeFitting> fittings, HashSet<int> cast,
        Dictionary<int, string> performerName, Dictionary<int, Gender?> performerGender)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(t =>
                {
                    t.Span($"{runningNumber}. ").SemiBold().FontColor(Colors.Grey.Darken1);
                    t.Span(string.IsNullOrWhiteSpace(number.Title) ? "Untitled number" : number.Title)
                        .FontSize(13).SemiBold().FontColor(Colors.Black);
                });
                if (costumes.Count > 0)
                    row.ConstantItem(180).AlignRight()
                        .Text(string.Join(" · ", costumes.Select(LookName)))
                        .SemiBold().FontColor(Colors.Grey.Darken2);
            });

            // The costumes this number wears.
            if (costumes.Count > 0)
            {
                col.Item().PaddingTop(4).Column(pc =>
                {
                    foreach (var costume in costumes)
                        pc.Item().PaddingTop(2).Element(c => ComposePiece(c, costume));
                });
            }

            // Who wears it: cast + on-stage extras, with sizes/notes. When looks are
            // in use (different costumes in one number), group wearers by their look.
            var byPerformer = assignments.ToDictionary(a => a.PerformerId);
            var wearerIds = cast.Union(assignments.Select(a => a.PerformerId)).ToList();
            if (wearerIds.Count > 0)
            {
                col.Item().PaddingTop(6).Text("Who wears it").FontSize(9).SemiBold().FontColor(Colors.Grey.Darken2);

                string Named(int id) => performerName.GetValueOrDefault(id, $"#{id}");
                // Split the wearers by costume only when there's more than one to
                // split between; a single-costume number is just a flat list.
                if (costumes.Count > 1)
                {
                    foreach (var costume in costumes)
                    {
                        var ids = wearerIds
                            .Where(id => byPerformer.TryGetValue(id, out var a) && a.CostumeId == costume.Id)
                            .OrderBy(Named).ToList();
                        if (ids.Count == 0) continue;
                        col.Item().PaddingTop(3).Text(LookName(costume)).FontSize(9).SemiBold().FontColor(Colors.Grey.Darken3);
                        foreach (var id in ids)
                            col.Item().Element(c => ComposeWearer(c, id, cast, Fit(costume.Id, id), Named));
                    }
                    var noLook = wearerIds
                        .Where(id => !byPerformer.TryGetValue(id, out var a) || a.CostumeId is null)
                        .OrderBy(Named).ToList();
                    if (noLook.Count > 0)
                    {
                        col.Item().PaddingTop(3).Text("No costume assigned").FontSize(9).SemiBold().FontColor(Colors.Grey.Medium);
                        foreach (var id in noLook) col.Item().Element(c => ComposeWearer(c, id, cast, null, Named));
                    }
                }
                else
                {
                    // One costume dresses everyone here, so they all need fitting in it.
                    var only = costumes.Count == 1 ? costumes[0].Id : (int?)null;
                    foreach (var id in wearerIds.OrderBy(Named))
                        col.Item().Element(c => ComposeWearer(
                            c, id, cast, only is int oc ? Fit(oc, id) : null, Named));
                }

                CostumeFitting? Fit(int costumeId, int performerId) =>
                    fittings.TryGetValue((costumeId, performerId), out var f) ? f : new CostumeFitting();
            }
        });
    }

    private static string LookName(Costume c) => CostumeChanges.LookName(c);

    /// <summary>
    /// One wearer's line. <paramref name="fit"/> is their fit in the costume they
    /// wear here (null when we can't tell which costume that is, so no fitting is
    /// claimed either way).
    /// </summary>
    private void ComposeWearer(
        IContainer container, int id, HashSet<int> cast,
        CostumeFitting? fit, Func<int, string> named)
    {
        var extra = !cast.Contains(id);
        container.Text(t =>
        {
            t.Span($"• {named(id)}").FontColor(Colors.Grey.Darken4);
            if (extra) t.Span("  (on stage, not in number)").FontSize(8).FontColor(Colors.Orange.Darken2);
            if (!string.IsNullOrWhiteSpace(fit?.Size)) t.Span($"  — size {fit!.Size}").FontSize(9).FontColor(Colors.Grey.Darken1);
            if (!string.IsNullOrWhiteSpace(fit?.Notes)) t.Span($"  ({fit!.Notes})").FontSize(9).Italic().FontColor(Colors.Grey.Medium);
            if (fit is { IsFitted: false })
                t.Span("  needs fitting").FontSize(8).Italic().FontColor(Colors.Red.Medium);
        });
    }

    private void ComposePiece(IContainer container, Costume p)
    {
        container.Column(col =>
        {
            var label = string.IsNullOrWhiteSpace(p.Name) ? "" : p.Name.Trim() + " ";
            col.Item().Text(t =>
            {
                t.Span(label).SemiBold().FontColor(Colors.Grey.Darken2);
                t.Span(string.IsNullOrWhiteSpace(p.Description) ? "(no description)" : p.Description!)
                    .FontColor(Colors.Black);
                // Only call out what still needs doing — "Ready" is the quiet default.
                if (p.Status != CostumeStatus.Ready)
                    t.Span($"   {(p.Status == CostumeStatus.Needed ? "STILL NEEDED" : "sourced")}")
                        .FontSize(8).SemiBold()
                        .FontColor(p.Status == CostumeStatus.Needed ? Colors.Red.Medium : Colors.Orange.Darken2);
            });
            if (!string.IsNullOrWhiteSpace(p.Accessories))
                col.Item().Text($"   Accessories: {p.Accessories}").FontSize(9).FontColor(Colors.Grey.Darken1);
            if (!string.IsNullOrWhiteSpace(p.Shoes))
                col.Item().Text($"   Shoes: {p.Shoes}").FontSize(9).FontColor(Colors.Grey.Darken1);
            if (!string.IsNullOrWhiteSpace(p.VendorUrl))
                col.Item().Text($"   Vendor: {p.VendorUrl}").FontSize(8.5f).FontColor(Colors.Blue.Medium);
            if (!string.IsNullOrWhiteSpace(p.PhotoUrl))
                col.Item().Text($"   Photo: {p.PhotoUrl}").FontSize(8.5f).FontColor(Colors.Blue.Medium);
        });
    }
}
