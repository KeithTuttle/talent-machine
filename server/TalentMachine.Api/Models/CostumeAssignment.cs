namespace TalentMachine.Api.Models;

/// <summary>
/// A performer's costume detail for a number: their size and any alteration
/// notes. A performer WITHOUT a NumberCast row for the same number is an
/// "on-stage extra" (on stage during the number but not performing it) — the
/// backstage sheet still needs their costume. Surrogate key; unique per
/// (number, performer).
/// </summary>
public class CostumeAssignment : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int MusicalNumberId { get; set; }
    public int PerformerId { get; set; }
    public string? Size { get; set; }
    public string? Notes { get; set; }

    public MusicalNumber? MusicalNumber { get; set; }
    public Performer? Performer { get; set; }
}
