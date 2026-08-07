namespace TalentMachine.Api.Models;

/// <summary>
/// Which costume a performer wears in a particular number. Only needed when a
/// number has more than one costume to choose between — with a single costume
/// everyone in the number wears it implicitly. A performer WITHOUT a NumberCast
/// row for the same number is an "on-stage extra" (on stage during the number but
/// not performing it), and a row here is what puts them on the backstage sheet.
///
/// Size, alteration notes and fitting live on <see cref="CostumeFitting"/> — they
/// belong to the costume, not to each number it's worn in.
/// </summary>
public class CostumeAssignment : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int MusicalNumberId { get; set; }
    public int PerformerId { get; set; }
    /// <summary>Which catalog costume this performer wears here; null = unassigned.</summary>
    public int? CostumeId { get; set; }

    public MusicalNumber? MusicalNumber { get; set; }
    public Performer? Performer { get; set; }
    public Costume? Costume { get; set; }
}
