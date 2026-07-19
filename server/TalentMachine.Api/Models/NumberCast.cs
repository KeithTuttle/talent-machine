namespace TalentMachine.Api.Models;

/// <summary>
/// Per-number casting: which performers appear in which musical number.
/// Composite key (MusicalNumberId, PerformerId) — no store-generated Id, so the
/// AppDbContext key-reset guard leaves these rows untouched.
/// </summary>
public class NumberCast : ITenantScoped
{
    public int TenantId { get; set; }
    public int MusicalNumberId { get; set; }
    public int PerformerId { get; set; }

    public MusicalNumber? MusicalNumber { get; set; }
    public Performer? Performer { get; set; }
}
