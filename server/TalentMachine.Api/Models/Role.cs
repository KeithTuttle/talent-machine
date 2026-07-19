namespace TalentMachine.Api.Models;

/// <summary>
/// A named character in a production ("Annie", "Miss Hannigan"), optionally
/// assigned to a person. Unassigned roles (PersonId null) are normal during
/// casting.
/// </summary>
public class Role : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int ProductionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? PersonId { get; set; }
    public string? Notes { get; set; }
    public int OrderIndex { get; set; }

    public Person? Person { get; set; }
}
