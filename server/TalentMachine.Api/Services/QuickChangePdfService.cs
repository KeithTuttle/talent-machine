using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Services;

/// <summary>Data for the backstage quick-change sheet.</summary>
public class QuickChangeData
{
    public string ProductionTitle { get; set; } = string.Empty;
    public List<Act> Acts { get; set; } = new();
    public List<MusicalNumber> Numbers { get; set; } = new();
    public List<NumberCast> NumberCasts { get; set; } = new();
    public List<CostumeAssignment> Assignments { get; set; } = new();
    public List<CostumePiece> Pieces { get; set; } = new();
    public List<Performer> Performers { get; set; } = new();
}

/// <summary>
/// The sheet the dressers hold in the wings: every costume change in running
/// order, urgent ones first, with blank Where / Dresser columns to pencil in
/// during tech.
/// </summary>
public class QuickChangePdfService
{
    public byte[] Build(QuickChangeData data)
    {
        var performerName = data.Performers.ToDictionary(p => p.Id, p => $"{p.FirstName} {p.LastName}".Trim());
        var changes = CostumeChanges.Detect(
            data.Acts, data.Numbers, data.NumberCasts, data.Assignments, data.Pieces);

        // Kids changing at the same moment (same two numbers, same costumes) share
        // a row — that's how it happens backstage.
        var moments = changes
            .GroupBy(c => (c.From.Id, c.To.Id, c.FromCostume, c.ToCostume))
            .Select(g => new
            {
                g.First().From,
                g.First().To,
                g.First().FromCostume,
                g.First().ToCostume,
                g.First().Buffer,
                Who = g.Select(c => performerName.GetValueOrDefault(c.PerformerId, $"#{c.PerformerId}"))
                       .OrderBy(n => n).ToList(),
            })
            .OrderBy(m => m.Buffer)
            .ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken4));

                page.Header().Column(col =>
                {
                    col.Item().Text("Quick Changes").FontSize(20).SemiBold().FontColor(Colors.Black);
                    col.Item().Text(data.ProductionTitle).FontColor(Colors.Grey.Medium);
                    col.Item().Text("Most urgent first. \"Back-to-back\" means they leave one number and enter the next — have a dresser ready.")
                        .FontSize(9).Italic().FontColor(Colors.Grey.Medium);
                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(8).Column(col =>
                {
                    if (moments.Count == 0)
                    {
                        col.Item().Text("No costume changes — everyone stays in the same costume throughout.")
                            .Italic().FontColor(Colors.Grey.Medium);
                        return;
                    }

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(70);  // urgency
                            c.RelativeColumn(2);   // after / before
                            c.RelativeColumn(2);   // out of / into
                            c.RelativeColumn(3);   // who
                            c.RelativeColumn(2);   // where (blank)
                            c.RelativeColumn(2);   // dresser (blank)
                        });

                        table.Header(h =>
                        {
                            void Head(string t) => h.Cell().Background(Colors.Grey.Lighten3).Padding(4)
                                .Text(t).FontSize(9).SemiBold().FontColor(Colors.Black);
                            Head("How long");
                            Head("Leaves → enters");
                            Head("Out of → into");
                            Head("Who");
                            Head("Where");
                            Head("Dresser");
                        });

                        foreach (var m in moments)
                        {
                            var (label, color) = m.Buffer switch
                            {
                                0 => ("BACK-TO-BACK", Colors.Red.Darken1),
                                1 => ("1 number", Colors.Orange.Darken2),
                                _ => ($"{m.Buffer} numbers", Colors.Green.Darken1),
                            };

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4)
                                .Text(label).FontSize(9).SemiBold().FontColor(color);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4)
                                .Text($"{Title(m.From)}  →  {Title(m.To)}").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4)
                                .Text($"{m.FromCostume}  →  {m.ToCostume}").FontSize(9).SemiBold();
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4)
                                .Text(string.Join(", ", m.Who)).FontSize(9);
                            // Filled in by hand at tech.
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text("");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text("");
                        }
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Quick changes — generated ");
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

    private static string Title(MusicalNumber n) =>
        string.IsNullOrWhiteSpace(n.Title) ? "Untitled number" : n.Title;
}
