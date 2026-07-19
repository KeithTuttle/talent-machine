namespace TalentMachine.Api.Models;

/// <summary>
/// Per-number casting: which people perform in which musical number.
/// Composite key (MusicalNumberId, PersonId) — no store-generated Id, so the
/// AppDbContext key-reset guard leaves these rows untouched.
/// </summary>
public class NumberCast : ITenantScoped
{
    public int TenantId { get; set; }
    public int MusicalNumberId { get; set; }
    public int PersonId { get; set; }

    public MusicalNumber? MusicalNumber { get; set; }
    public Person? Person { get; set; }
}
