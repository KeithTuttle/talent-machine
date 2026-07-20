namespace TalentMachine.Api.Models;

/// <summary>
/// An act of the show ("Act 1", "Act 2"). Numbers hang off an act via
/// MusicalNumber.ActId; the running order is numbers ordered within their act.
/// </summary>
public class Act : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int ProductionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
}
