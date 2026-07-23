using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Services;

/// <summary>Data for the backstage props sheet.</summary>
public class PropSheetData
{
    public string ProductionTitle { get; set; } = string.Empty;
    public List<Act> Acts { get; set; } = new();
    public List<Scene> Scenes { get; set; } = new();
    public List<Prop> Props { get; set; } = new();
    public List<PropAssignment> Assignments { get; set; } = new();
}

/// <summary>
/// A high-contrast, backstage-readable props sheet in two parts: a master pull
/// list (every prop with quantity, where it's stored, and gathering status —
/// what to grab before the show), then a run-of-show section walking Act → Scene
/// in order, with each scene's props: quantity, where it's preset, who brings it
/// on, and where it strikes. Built to be scanned quickly in the wings.
/// </summary>
public class PropsPdfService
{
    public byte[] Build(PropSheetData data)
    {
        var propById = data.Props.ToDictionary(p => p.Id);
        var assignmentsByScene = data.Assignments.ToLookup(a => a.SceneId);

        // Scenes in running order: by act (ordered), then scene order; actless last.
        var orderedScenes = new List<Scene>();
        foreach (var act in data.Acts.OrderBy(a => a.OrderIndex).ThenBy(a => a.Id))
            orderedScenes.AddRange(data.Scenes.Where(s => s.ActId == act.Id)
                .OrderBy(s => s.OrderIndex).ThenBy(s => s.Id));
        orderedScenes.AddRange(data.Scenes.Where(s => s.ActId == null)
            .OrderBy(s => s.OrderIndex).ThenBy(s => s.Id));
        var actName = data.Acts.ToDictionary(a => a.Id, a => a.Name);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken4));

                page.Header().Column(col =>
                {
                    col.Item().Text("Props Sheet").FontSize(20).SemiBold().FontColor(Colors.Black);
                    col.Item().Text(data.ProductionTitle).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(14);

                    // --- Master pull list ---
                    col.Item().Element(c => ComposePullList(c, data.Props));

                    // --- Run of show ---
                    col.Item().PaddingTop(4).Text("Run of Show").FontSize(14).SemiBold().FontColor(Colors.Black);
                    if (orderedScenes.Count == 0)
                    {
                        col.Item().Text("No scenes yet — add scenes in Script to lay out prop cues.")
                            .Italic().FontColor(Colors.Grey.Medium);
                    }
                    foreach (var scene in orderedScenes)
                    {
                        var rows = assignmentsByScene[scene.Id].ToList();
                        col.Item().Element(c => ComposeScene(c, scene, actName, rows, propById));
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Props sheet — generated ");
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

    private void ComposePullList(IContainer container, List<Prop> props)
    {
        container.Column(col =>
        {
            col.Item().Text("Master Pull List").FontSize(14).SemiBold().FontColor(Colors.Black);
            if (props.Count == 0)
            {
                col.Item().Text("No props yet.").Italic().FontColor(Colors.Grey.Medium);
                return;
            }
            col.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(4); // name
                    c.ConstantColumn(40); // qty
                    c.RelativeColumn(4); // storage
                    c.ConstantColumn(70); // status
                });

                void HeaderCell(string t) => table.Cell().Element(CellStyleHeader).Text(t).SemiBold();
                HeaderCell("Prop");
                HeaderCell("Qty");
                HeaderCell("Stored");
                HeaderCell("Status");

                foreach (var p in props.OrderBy(p => p.Name))
                {
                    table.Cell().Element(CellStyle).Text(t =>
                    {
                        t.Span(string.IsNullOrWhiteSpace(p.Name) ? "Untitled prop" : p.Name).SemiBold();
                        if (!string.IsNullOrWhiteSpace(p.Description))
                            t.Span($"  — {p.Description}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                    table.Cell().Element(CellStyle).AlignCenter().Text(p.Quantity.ToString());
                    table.Cell().Element(CellStyle).Text(
                        string.IsNullOrWhiteSpace(p.StorageLocation) ? "—" : p.StorageLocation!);
                    table.Cell().Element(CellStyle).Text(p.Status.ToString())
                        .FontColor(StatusColor(p.Status)).SemiBold();
                }
            });
        });

        static IContainer CellStyleHeader(IContainer c) =>
            c.Background(Colors.Grey.Lighten3).BorderBottom(1).BorderColor(Colors.Grey.Lighten1).PaddingVertical(4).PaddingHorizontal(6);
        static IContainer CellStyle(IContainer c) =>
            c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).PaddingHorizontal(6);
    }

    private void ComposeScene(
        IContainer container, Scene scene, Dictionary<int, string> actName,
        List<PropAssignment> rows, Dictionary<int, Prop> propById)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(col =>
        {
            var act = scene.ActId is { } aid && actName.TryGetValue(aid, out var an) ? an : "Unassigned";
            col.Item().Text(t =>
            {
                t.Span($"{act} · ").FontColor(Colors.Grey.Darken1);
                t.Span(string.IsNullOrWhiteSpace(scene.Name) ? "Scene" : scene.Name)
                    .FontSize(12).SemiBold().FontColor(Colors.Black);
                if (!string.IsNullOrWhiteSpace(scene.Setting))
                    t.Span($"  ({scene.Setting})").FontColor(Colors.Grey.Medium);
            });

            if (rows.Count == 0)
            {
                col.Item().PaddingTop(2).Text("No props in this scene.").Italic().FontSize(9).FontColor(Colors.Grey.Medium);
                return;
            }

            col.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3); // prop
                    c.ConstantColumn(32); // qty
                    c.RelativeColumn(3); // preset
                    c.RelativeColumn(2); // handler
                    c.RelativeColumn(3); // strike / notes
                });

                void H(string t) => table.Cell().Element(HeadCell).Text(t).SemiBold().FontSize(9);
                H("Prop");
                H("Qty");
                H("Preset");
                H("Brought on by");
                H("Strike / notes");

                foreach (var r in rows.OrderBy(r => propById.GetValueOrDefault(r.PropId)?.Name ?? ""))
                {
                    var prop = propById.GetValueOrDefault(r.PropId);
                    table.Cell().Element(BodyCell).Text(prop?.Name ?? "—").SemiBold();
                    table.Cell().Element(BodyCell).AlignCenter().Text((prop?.Quantity ?? 1).ToString());
                    table.Cell().Element(BodyCell).Text(string.IsNullOrWhiteSpace(r.PresetLocation) ? "—" : r.PresetLocation!);
                    table.Cell().Element(BodyCell).Text(string.IsNullOrWhiteSpace(r.Handler) ? "—" : r.Handler!);
                    table.Cell().Element(BodyCell).Text(t =>
                    {
                        var strike = string.IsNullOrWhiteSpace(r.StrikeTo) ? "" : r.StrikeTo!;
                        var notes = string.IsNullOrWhiteSpace(r.Notes) ? "" : r.Notes!;
                        if (strike.Length > 0) t.Span(strike);
                        if (strike.Length > 0 && notes.Length > 0) t.Span("  ");
                        if (notes.Length > 0) t.Span(notes).Italic().FontColor(Colors.Grey.Darken1);
                        if (strike.Length == 0 && notes.Length == 0) t.Span("—");
                    });
                }
            });
        });

        static IContainer HeadCell(IContainer c) =>
            c.Background(Colors.Grey.Lighten3).BorderBottom(1).BorderColor(Colors.Grey.Lighten1).PaddingVertical(3).PaddingHorizontal(5);
        static IContainer BodyCell(IContainer c) =>
            c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).PaddingHorizontal(5);
    }

    private static string StatusColor(PropStatus status) => status switch
    {
        PropStatus.Ready => Colors.Green.Darken1,
        PropStatus.Sourced => Colors.Blue.Medium,
        _ => Colors.Orange.Darken2,
    };
}
