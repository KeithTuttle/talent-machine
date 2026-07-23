using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TalentMachine.Api.Models;

namespace TalentMachine.Api.Services;

/// <summary>Data used to render the rehearsal-schedule PDF.</summary>
public class RehearsalPdfData
{
    public string ProductionTitle { get; set; } = string.Empty;
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public List<Rehearsal> Slots { get; set; } = new();
    public List<RehearsalAttendee> Overrides { get; set; } = new();
    public List<NumberCast> NumberCasts { get; set; } = new();
    public List<MusicalNumber> Numbers { get; set; } = new();
    public List<Performer> Performers { get; set; } = new();
    public List<Conflict> Conflicts { get; set; } = new();
}

/// <summary>
/// Printable rehearsal schedule: slots grouped by day, each with its time range,
/// type, number, resolved attendees, and a ⚠ marker on kids whose conflicts hit
/// that day. Handed out / emailed weekly.
/// </summary>
public class RehearsalPdfService
{
    public byte[] Build(RehearsalPdfData data)
    {
        var castByNumber = data.NumberCasts.ToLookup(c => c.MusicalNumberId, c => c.PerformerId);
        var numberTitle = data.Numbers.ToDictionary(n => n.Id, n => n.Title);
        var performerName = data.Performers.ToDictionary(
            p => p.Id, p => $"{p.FirstName} {p.LastName}".Trim());

        var days = data.Slots
            .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
            .GroupBy(s => s.Date)
            .ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                page.Header().Column(col =>
                {
                    col.Item().Text("Rehearsal Schedule").FontSize(20).SemiBold().FontColor(Colors.Black);
                    col.Item().Text(t =>
                    {
                        t.Span(data.ProductionTitle).FontColor(Colors.Grey.Medium);
                        t.Span("   •   ").FontColor(Colors.Grey.Medium);
                        t.Span($"{data.From:MMM d} – {data.To:MMM d, yyyy}").FontColor(Colors.Grey.Medium);
                    });
                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(14);

                    if (days.Count == 0)
                    {
                        col.Item().Text("No rehearsals scheduled for this range.")
                            .Italic().FontColor(Colors.Grey.Medium);
                        return;
                    }

                    foreach (var day in days)
                    {
                        col.Item().Element(c => ComposeDay(
                            c, day.Key, day.ToList(), castByNumber, numberTitle, performerName, data));
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Generated ");
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

    private void ComposeDay(
        IContainer container,
        DateOnly date,
        List<Rehearsal> slots,
        ILookup<int, int> castByNumber,
        Dictionary<int, string> numberTitle,
        Dictionary<int, string> performerName,
        RehearsalPdfData data)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(2)
                .Text(date.ToString("dddd, MMMM d")).FontSize(13).SemiBold().FontColor(Colors.Black);

            foreach (var slot in slots)
            {
                var attendees = RehearsalResolver.ResolveAttendees(slot, castByNumber, data.Overrides);
                var conflicted = attendees
                    .Where(pid => data.Conflicts.Any(c =>
                        c.PerformerId == pid && RehearsalResolver.ConflictOccursOn(c, date)))
                    .ToHashSet();

                var title = slot.MusicalNumberId is int nid && numberTitle.TryGetValue(nid, out var t)
                    ? t : "General";

                col.Item().PaddingTop(4).Row(row =>
                {
                    row.ConstantItem(92).Text($"{Fmt(slot.StartTime)}–{Fmt(slot.EndTime)}")
                        .SemiBold().FontColor(Colors.Grey.Darken1);
                    row.RelativeItem().Column(info =>
                    {
                        info.Item().Text(txt =>
                        {
                            txt.Span(title).SemiBold().FontColor(Colors.Black);
                            txt.Span($"   {slot.Type}").FontSize(9).FontColor(Colors.Grey.Medium);
                            if (!string.IsNullOrWhiteSpace(slot.Room))
                                txt.Span($"   •  {slot.Room}").FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                        if (attendees.Count > 0)
                        {
                            var names = attendees
                                .Select(id =>
                                {
                                    var name = performerName.TryGetValue(id, out var n) ? n : $"#{id}";
                                    return conflicted.Contains(id) ? $"⚠ {name}" : name;
                                })
                                .OrderBy(n => n.TrimStart('⚠', ' '));
                            info.Item().PaddingTop(1).Text(string.Join(", ", names))
                                .FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                        }
                        if (conflicted.Count > 0)
                        {
                            info.Item().PaddingTop(1)
                                .Text("⚠ marked performers have a conflict this day")
                                .FontSize(8).FontColor(Colors.Orange.Darken2);
                        }
                        if (!string.IsNullOrWhiteSpace(slot.Notes))
                            info.Item().PaddingTop(1).Text(slot.Notes!)
                                .FontSize(8.5f).Italic().FontColor(Colors.Grey.Medium);
                    });
                });
            }
        });
    }

    private static string Fmt(TimeOnly t) => t.ToString("h:mm tt");
}
