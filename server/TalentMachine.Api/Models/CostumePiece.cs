namespace TalentMachine.Api.Models;

/// <summary>
/// A costume spec for a number — what a group wears. Optionally gendered (Boys /
/// Girls / All) so a number can have distinct looks. Photos and vendor links help
/// backstage helpers identify and source pieces.
/// </summary>
public class CostumePiece : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int MusicalNumberId { get; set; }
    public CostumeGender Gender { get; set; } = CostumeGender.All;
    public string? Description { get; set; }
    public string? Accessories { get; set; }
    public string? Shoes { get; set; }
    public string? PhotoUrl { get; set; }
    public string? VendorUrl { get; set; }

    public MusicalNumber? MusicalNumber { get; set; }
}
