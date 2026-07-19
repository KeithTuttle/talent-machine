namespace TalentMachine.Api.Models;

/// <summary>
/// A named production session within a year, e.g. "Summer 2026". Multiple seasons
/// per year are allowed; a season holds one or more productions and can be
/// archived once its shows have closed (multi-year history is a core feature).
/// </summary>
public class Season : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int Year { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
