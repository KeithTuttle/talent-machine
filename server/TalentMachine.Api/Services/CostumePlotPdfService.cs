using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Services;

/// <summary>Data for the per-performer costume plot.</summary>
public class CostumePlotData
{
    public string ProductionTitle { get; set; } = string.Empty;
    public List<Act> Acts { get; set; } = new();
    public List<MusicalNumber> Numbers { get; set; } = new();
    public List<NumberCast> NumberCasts { get; set; } = new();
    public List<CostumeAssignment> Assignments { get; set; } = new();
    public List<Costume> Costumes { get; set; } = new();
    public List<Performer> Performers { get; set; } = new();
    public List<CastMembership> Cast { get; set; } = new();
}

/// <summary>
/// One block per kid: every number they're in, in running order, and what they
/// wear for it — the sheet a parent can follow. Changes are called out so they
/// know when to be ready with the next costume.
/// </summary>
public class CostumePlotPdfService
{
    public byte[] Build(CostumePlotData data)
    {
        var ordered = CostumeChanges.RunningOrder(data.Acts, data.Numbers);
        var position = new Dictionary<int, int>();
        for (var i = 0; i < ordered.Count; i++) position[ordered[i].Id] = i;

        var costumeById = data.Costumes.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
        var assignmentByKey = data.Assignments
            .GroupBy(a => (a.MusicalNumberId, a.PerformerId))
            .ToDictionary(g => g.Key, g => g.First());
        var actName = data.Acts.ToDictionary(a => a.Id, a => a.Name);

        // Everyone in the show, alphabetical — the order a parent scans for their kid.
        var performerById = data.Performers.ToDictionary(p => p.Id);
        var people = data.Cast
            .Select(m => performerById.TryGetValue(m.PerformerId, out var p) ? p : null)
            .Where(p => p is not null).Select(p => p!)
            .DistinctBy(p => p.Id)
            .OrderBy(p => $"{p.FirstName} {p.LastName}".Trim())
            .ToList();

        var castByPerformer = data.NumberCasts.ToLookup(c => c.PerformerId, c => c.MusicalNumberId);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken4));

                page.Header().Column(col =>
                {
                    col.Item().Text("Costume Plot — by performer").FontSize(20).SemiBold().FontColor(Colors.Black);
                    col.Item().Text(data.ProductionTitle).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(10);
                    if (people.Count == 0)
                    {
                        col.Item().Text("Nobody is in the cast yet.").Italic().FontColor(Colors.Grey.Medium);
                        return;
                    }

                    foreach (var p in people)
                    {
                        var appearances = castByPerformer[p.Id]
                            .Where(position.ContainsKey)
                            .Distinct()
                            .OrderBy(id => position[id])
                            .Select(id => ordered[position[id]])
                            .ToList();

                        col.Item().Element(c => ComposePerformer(
                            c, p, appearances, assignmentByKey, costumeById, actName));
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Costume plot — generated ");
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

    private void ComposePerformer(
        IContainer container, Performer performer, List<MusicalNumber> appearances,
        Dictionary<(int, int), CostumeAssignment> assignments,
        Dictionary<int, Costume> costumes,
        Dictionary<int, string> actName)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(col =>
        {
            col.Item().Text($"{performer.FirstName} {performer.LastName}".Trim())
                .FontSize(13).SemiBold().FontColor(Colors.Black);

            if (appearances.Count == 0)
            {
                col.Item().Text("Not cast in any number yet.").FontSize(9).Italic().FontColor(Colors.Grey.Medium);
                return;
            }

            string? previous = null;
            foreach (var n in appearances)
            {
                assignments.TryGetValue((n.Id, performer.Id), out var a);
                var costume = a?.CostumeId is int cid && costumes.TryGetValue(cid, out var entry)
                    ? CostumeChanges.LookName(entry)
                    : n.CostumeLabel?.Trim();
                var changed = previous is not null && !string.IsNullOrWhiteSpace(costume)
                    && !string.Equals(previous, costume, StringComparison.OrdinalIgnoreCase);

                col.Item().PaddingTop(2).Text(t =>
                {
                    var act = n.ActId is int actId && actName.TryGetValue(actId, out var an) ? $"{an} · " : "";
                    t.Span($"{act}{(string.IsNullOrWhiteSpace(n.Title) ? "Untitled number" : n.Title)}")
                        .FontColor(Colors.Grey.Darken4);
                    t.Span("  —  ").FontColor(Colors.Grey.Lighten1);
                    t.Span(string.IsNullOrWhiteSpace(costume) ? "(costume not set)" : costume!)
                        .SemiBold().FontColor(string.IsNullOrWhiteSpace(costume) ? Colors.Grey.Medium : Colors.Black);
                    if (!string.IsNullOrWhiteSpace(a?.Size)) t.Span($"  size {a!.Size}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    if (changed) t.Span("   ← CHANGE").FontSize(8).SemiBold().FontColor(Colors.Orange.Darken2);
                    if (a is { CostumeId: not null, IsFitted: false })
                        t.Span("   needs fitting").FontSize(8).Italic().FontColor(Colors.Red.Medium);
                });

                if (!string.IsNullOrWhiteSpace(costume)) previous = costume;
            }
        });
    }
}
