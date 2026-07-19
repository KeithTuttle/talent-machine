namespace TalentMachine.Api.Models;

/// <summary>
/// Marker for entities isolated per tenant. AppDbContext applies a global query
/// filter (TenantId == current tenant) and stamps TenantId on insert for every
/// implementer, so tenant isolation is automatic and cannot be forgotten.
/// </summary>
public interface ITenantScoped
{
    int TenantId { get; set; }
}
