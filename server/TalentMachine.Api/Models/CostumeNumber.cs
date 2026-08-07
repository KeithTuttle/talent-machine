namespace TalentMachine.Api.Models;

/// <summary>
/// A catalog <see cref="Costume"/> used in a number — "this number wears these
/// costumes". Composite key, no surrogate id (mirrors NumberCast/NumberCharacter).
/// </summary>
public class CostumeNumber : ITenantScoped
{
    public int TenantId { get; set; }
    public int CostumeId { get; set; }
    public int MusicalNumberId { get; set; }

    public Costume? Costume { get; set; }
    public MusicalNumber? MusicalNumber { get; set; }
}
