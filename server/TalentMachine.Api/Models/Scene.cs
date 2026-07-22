namespace TalentMachine.Api.Models;

/// <summary>
/// A scene in the production's book — the dialogue/story unit that songs sit
/// inside ("Act 1, Scene 1 — Orphanage"). Belongs to an act (nullable, like a
/// number can be actless); numbers nest under a scene via MusicalNumber.SceneId.
/// The scene's characters (see <see cref="SceneCharacter"/>) drive "who's needed
/// when" for blocking rehearsals.
/// </summary>
public class Scene : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int ProductionId { get; set; }
    /// <summary>Which act this scene belongs to; null = not yet placed.</summary>
    public int? ActId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Where the scene takes place ("Orphanage", "NYC street").</summary>
    public string? Setting { get; set; }
    public string? Notes { get; set; }
    public int OrderIndex { get; set; }

    public Act? Act { get; set; }
}
