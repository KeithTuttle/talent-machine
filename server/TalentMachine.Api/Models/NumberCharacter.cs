namespace TalentMachine.Api.Models;

/// <summary>
/// A character (Role) featured in a musical number — an additive tag on top of
/// the performer-level <see cref="NumberCast"/> (which is left untouched).
/// Composite key (MusicalNumberId, RoleId), no store-generated Id.
/// </summary>
public class NumberCharacter : ITenantScoped
{
    public int TenantId { get; set; }
    public int MusicalNumberId { get; set; }
    public int RoleId { get; set; }

    public MusicalNumber? MusicalNumber { get; set; }
    public Role? Role { get; set; }
}
