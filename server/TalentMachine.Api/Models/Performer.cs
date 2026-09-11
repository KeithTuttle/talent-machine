namespace TalentMachine.Api.Models;

/// <summary>
/// A performer. Tenant-level identity — NOT tied to a production — so one
/// performer's history spans productions and years (via CastMembership rows).
/// </summary>
public class Performer : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Gender? Gender { get; set; }
    /// <summary>Optional; when set, the UI computes age as of a production's show date.</summary>
    public DateOnly? DateOfBirth { get; set; }
    /// <summary>
    /// A typed-in age, for when the birth date isn't known. Only used when
    /// <see cref="DateOfBirth"/> is null — it's a snapshot from whenever it was
    /// entered, not a birthday, so it doesn't advance on its own the way a
    /// DOB-derived age does.
    /// </summary>
    public int? AgeYears { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
