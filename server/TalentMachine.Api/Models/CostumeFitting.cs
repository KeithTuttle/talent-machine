namespace TalentMachine.Api.Models;

/// <summary>
/// One performer's fit in one costume: their size, any alteration notes, and
/// whether they've been fitted. Keyed on (costume, performer) — NOT per number —
/// because a fitting is a real-world event that happens once: fit a kid in the
/// street wear and they're fitted for it in every number that wears it.
/// </summary>
public class CostumeFitting : ITenantScoped
{
    public int TenantId { get; set; }
    public int CostumeId { get; set; }
    public int PerformerId { get; set; }
    public string? Size { get; set; }
    public string? Notes { get; set; }
    public bool IsFitted { get; set; }

    public Costume? Costume { get; set; }
    public Performer? Performer { get; set; }
}
