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
    public List<CostumePiece> Pieces { get; set; } = new();
    public List<CostumeAssignment> Assignments { get; set; } = new();
    public List<NumberCast> NumberCasts { get; set; } = new();
    public List<Performer> Performers { get; set; } = new();
}

/// <summary>
/// A high-contrast, backstage-readable costume sheet: every number in running
/// order with its costume label, the pieces (split Boys/Girls where set, with
/// accessories/shoes and vendor/photo links), and the kids who wear it — with
/// sizes and alteration notes, and on-stage extras (not in the number) flagged.
/// </summary>
public class CostumePdfService
{
    public byte[] Build(CostumeSheetData data)
    {
        var performerName = data.Performers.ToDictionary(p => p.Id, p => $"{p.FirstName} {p.LastName}".Trim());
        var performerGender = data.Performers.ToDictionary(p => p.Id, p => p.Gender);
        var castByNumber = data.NumberCasts.ToLookup(c => c.MusicalNumberId, c => c.PerformerId);
        var piecesByNumber = data.Pieces.ToLookup(p => p.MusicalNumberId);
        var assignmentsByNumber = data.Assignments.ToLookup(a => a.MusicalNumberId);

        // Numbers in running order: acts then unassigned.
        var ordered = new List<MusicalNumber>();
        foreach (var act in data.Acts.OrderBy(a => a.OrderIndex).ThenBy(a => a.Id))
            ordered.AddRange(data.Numbers.Where(n => n.ActId == act.Id).OrderBy(n => n.OrderIndex).ThenBy(n => n.Id));
        ordered.AddRange(data.Numbers.Where(n => n.ActId == null).OrderBy(n => n.OrderIndex).ThenBy(n => n.Id));

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
                            c, i + 1, n, piecesByNumber[n.Id].ToList(), assignmentsByNumber[n.Id].ToList(),
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
        List<CostumePiece> pieces, List<CostumeAssignment> assignments, HashSet<int> cast,
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
                if (!string.IsNullOrWhiteSpace(number.CostumeLabel))
                    row.ConstantItem(160).AlignRight().Text($"Costume: {number.CostumeLabel}")
                        .SemiBold().FontColor(Colors.Grey.Darken2);
            });

            // Pieces (specs).
            if (pieces.Count > 0)
            {
                col.Item().PaddingTop(4).Column(pc =>
                {
                    foreach (var p in pieces.OrderBy(p => p.Gender))
                        pc.Item().PaddingTop(2).Element(c => ComposePiece(c, p));
                });
            }

            // Who wears it: cast + on-stage extras, with sizes/notes.
            var byPerformer = assignments.ToDictionary(a => a.PerformerId);
            var wearerIds = cast.Union(assignments.Select(a => a.PerformerId)).ToList();
            if (wearerIds.Count > 0)
            {
                col.Item().PaddingTop(6).Text("Who wears it").FontSize(9).SemiBold().FontColor(Colors.Grey.Darken2);
                foreach (var id in wearerIds.OrderBy(id => performerName.GetValueOrDefault(id, $"#{id}")))
                {
                    var extra = !cast.Contains(id);
                    byPerformer.TryGetValue(id, out var a);
                    col.Item().Text(t =>
                    {
                        t.Span($"• {performerName.GetValueOrDefault(id, $"#{id}")}").FontColor(Colors.Grey.Darken4);
                        if (extra) t.Span("  (on stage, not in number)").FontSize(8).FontColor(Colors.Orange.Darken2);
                        if (!string.IsNullOrWhiteSpace(a?.Size)) t.Span($"  — size {a!.Size}").FontSize(9).FontColor(Colors.Grey.Darken1);
                        if (!string.IsNullOrWhiteSpace(a?.Notes)) t.Span($"  ({a!.Notes})").FontSize(9).Italic().FontColor(Colors.Grey.Medium);
                    });
                }
            }
        });
    }

    private void ComposePiece(IContainer container, CostumePiece p)
    {
        container.Column(col =>
        {
            var label = p.Gender == CostumeGender.All ? "" : $"[{p.Gender}] ";
            col.Item().Text(t =>
            {
                t.Span(label).SemiBold().FontColor(Colors.Grey.Darken2);
                t.Span(string.IsNullOrWhiteSpace(p.Description) ? "(no description)" : p.Description!)
                    .FontColor(Colors.Black);
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
