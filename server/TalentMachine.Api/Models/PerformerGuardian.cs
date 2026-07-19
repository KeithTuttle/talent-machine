namespace TalentMachine.Api.Models;

/// <summary>
/// Links a performer to a guardian (typically 1–2 per kid, not enforced).
/// Composite key — no store-generated Id, so the key-reset guard skips it.
/// </summary>
public class PerformerGuardian : ITenantScoped
{
    public int TenantId { get; set; }
    public int PerformerId { get; set; }
    public int GuardianId { get; set; }

    public Performer? Performer { get; set; }
    public Guardian? Guardian { get; set; }
}
