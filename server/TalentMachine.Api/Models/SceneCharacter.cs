namespace TalentMachine.Api.Models;

/// <summary>
/// A character (Role) present in a scene — the script breakdown of "who's in
/// this scene". Composite key (SceneId, RoleId), no store-generated Id.
/// </summary>
public class SceneCharacter : ITenantScoped
{
    public int TenantId { get; set; }
    public int SceneId { get; set; }
    public int RoleId { get; set; }

    public Scene? Scene { get; set; }
    public Role? Role { get; set; }
}
