namespace TalentMachine.Api.Models;

/// <summary>
/// A prop's use in one scene: where it's preset, who brings it on, and where it
/// goes when struck. Surrogate Id (not a composite key) so a prop can appear in a
/// scene more than once and each row edits with a simple PUT. Deleting the prop
/// or the scene removes the row.
/// </summary>
public class PropAssignment : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int PropId { get; set; }
    public int SceneId { get; set; }
    /// <summary>Where the prop starts the scene ("SR prop table", "preset on desk").</summary>
    public string? PresetLocation { get; set; }
    /// <summary>Who brings it on / handles it (a character, "SM", "preset"). Free text.</summary>
    public string? Handler { get; set; }
    /// <summary>Where it goes when struck ("back to SR table", "off SL").</summary>
    public string? StrikeTo { get; set; }
    public string? Notes { get; set; }

    public Prop? Prop { get; set; }
    public Scene? Scene { get; set; }
}
