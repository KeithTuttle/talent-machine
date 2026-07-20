namespace TalentMachine.Api.Models;

/// <summary>A show staged within a season (e.g. "Annie", Summer 2026).</summary>
public class Production : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public string Title { get; set; } = string.Empty;
    /// <summary>Optional opening/performance date; ages are computed as of this
    /// date (falling back to today) so past shows keep historically-true ages.</summary>
    public DateOnly? OpeningDate { get; set; }
    public string? Notes { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Season? Season { get; set; }
}
