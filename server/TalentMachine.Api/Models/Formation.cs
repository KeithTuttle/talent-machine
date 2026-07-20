namespace TalentMachine.Api.Models;

/// <summary>
/// A stage formation for a number: named positions of its performers. A number
/// has many formations (the choreography's moments), kept in order — this is the
/// history directors want to look back on. Coordinates is a JSON map
/// <c>{ performerId: { x, y } }</c> in 0–100 stage percentages.
/// </summary>
public class Formation : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int MusicalNumberId { get; set; }
    public string FormationName { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public string Coordinates { get; set; } = "{}";

    public MusicalNumber? MusicalNumber { get; set; }
}
