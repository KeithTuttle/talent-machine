using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Services;

/// <summary>Data used to render the show running-order PDF.</summary>
public class ShowOrderData
{
    public string ProductionTitle { get; set; } = string.Empty;
    public DateOnly? OpeningDate { get; set; }
    public List<Act> Acts { get; set; } = new();
    public List<MusicalNumber> Numbers { get; set; } = new();
    public List<NumberCast> NumberCasts { get; set; } = new();
    public List<Performer> Performers { get; set; } = new();
    public Dictionary<int, string> StaffNames { get; set; } = new();
}

/// <summary>
/// Printable running order: numbers grouped by act, each with its running number
/// (continuing across acts), title, songwriter, and cast. Per-number production
/// notes (lighting, sets, …) will slot into ComposeNumber when that detail is
/// modeled — this layout deliberately leaves room under each number for them.
/// </summary>
public class ShowOrderPdfService
{
    private Dictionary<int, string> _staffNames = new();

    public byte[] Build(ShowOrderData data)
    {
        _staffNames = data.StaffNames;
        var castByNumber = data.NumberCasts.ToLookup(c => c.MusicalNumberId, c => c.PerformerId);
        var performerName = data.Performers.ToDictionary(
            p => p.Id, p => $"{p.FirstName} {p.LastName}".Trim());

        // Groups: acts in order, then an Unassigned bucket if used. Running
        // numbers are precomputed per group so the count isn't threaded through
        // QuestPDF's deferred composition lambdas.
        var groups = new List<(string? Header, List<MusicalNumber> Entries, int StartNumber)>();
        var running = 0;
        foreach (var act in data.Acts.OrderBy(a => a.OrderIndex).ThenBy(a => a.Id))
        {
            var entries = data.Numbers.Where(n => n.ActId == act.Id)
                .OrderBy(n => n.OrderIndex).ThenBy(n => n.Id).ToList();
            groups.Add((act.Name, entries, running));
            running += entries.Count;
        }
        var unassigned = data.Numbers.Where(n => n.ActId == null)
            .OrderBy(n => n.OrderIndex).ThenBy(n => n.Id).ToList();
        if (unassigned.Count > 0)
            groups.Add((data.Acts.Count > 0 ? "Unassigned" : null, unassigned, running));

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                page.Header().Column(col =>
                {
                    col.Item().Text("Running Order").FontSize(20).SemiBold().FontColor(Colors.Black);
                    col.Item().Text(t =>
                    {
                        t.Span(data.ProductionTitle).FontColor(Colors.Grey.Medium);
                        if (data.OpeningDate is not null)
                        {
                            t.Span("   •   ").FontColor(Colors.Grey.Medium);
                            t.Span(data.OpeningDate.Value.ToString("MMMM d, yyyy")).FontColor(Colors.Grey.Medium);
                        }
                    });
                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(14);

                    if (data.Numbers.Count == 0)
                    {
                        col.Item().Text("No numbers in this production yet.")
                            .Italic().FontColor(Colors.Grey.Medium);
                        return;
                    }

                    foreach (var (header, entries, startNumber) in groups)
                    {
                        if (entries.Count == 0) continue;
                        col.Item().Element(c => ComposeAct(
                            c, header, entries, startNumber, castByNumber, performerName));
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Running order — generated ");
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

    private void ComposeAct(
        IContainer container,
        string? header,
        List<MusicalNumber> entries,
        int startNumber,
        ILookup<int, int> castByNumber,
        Dictionary<int, string> performerName)
    {
        container.Column(col =>
        {
            if (!string.IsNullOrWhiteSpace(header))
                col.Item().PaddingBottom(2).Text(header!).FontSize(13).SemiBold().FontColor(Colors.Black);

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var number = startNumber + i + 1;

                col.Item().PaddingTop(i == 0 ? 0 : 4).Row(row =>
                {
                    row.ConstantItem(28).Text($"{number}.").SemiBold().FontColor(Colors.Grey.Darken1);
                    row.RelativeItem().Column(info =>
                    {
                        info.Item().Text(string.IsNullOrWhiteSpace(entry.Title) ? "Untitled number" : entry.Title)
                            .SemiBold().FontColor(Colors.Black);
                        if (entry.ChoreographerStaffId is int cid && _staffNames.TryGetValue(cid, out var choreo))
                            info.Item().Text($"Choreographer: {choreo}").FontSize(9).FontColor(Colors.Grey.Medium);

                        var cast = castByNumber[entry.Id].ToList();
                        if (cast.Count > 0)
                        {
                            var names = cast
                                .Select(id => performerName.TryGetValue(id, out var n) ? n : $"#{id}")
                                .OrderBy(n => n);
                            info.Item().PaddingTop(1).Text(string.Join(", ", names))
                                .FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                        }
                        // Future: per-number production notes (lighting, sets, …) render here.
                    });
                });
            }
        });
    }
}
