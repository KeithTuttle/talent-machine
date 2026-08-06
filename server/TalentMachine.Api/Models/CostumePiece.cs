namespace TalentMachine.Api.Models;

/// <summary>
/// A costume spec for a number — what a group wears. Optionally gendered (Boys /
/// Girls / All) so a number can have distinct looks. A short <see cref="Label"/>
/// names the look (e.g. "Bear", "Soldier") so a single number can carry several
/// distinct costumes and each performer is assigned to one (see
/// <see cref="CostumeAssignment.CostumePieceId"/>). Photos and vendor links help
/// backstage helpers identify and source pieces.
/// </summary>
public class CostumePiece : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int MusicalNumberId { get; set; }
    /// <summary>Short name for this look (e.g. "Bear", "Soldier"); null = unnamed.</summary>
    public string? Label { get; set; }
    public CostumeGender Gender { get; set; } = CostumeGender.All;
    public string? Description { get; set; }
    public string? Accessories { get; set; }
    public string? Shoes { get; set; }
    public string? PhotoUrl { get; set; }
    public string? VendorUrl { get; set; }

    public MusicalNumber? MusicalNumber { get; set; }
}
