using Godot;

namespace TheDeep;

/// <summary>
/// One row of the architecture behind a room, as content states it (<see cref="BiomeProfile.ArchitectureRows"/> for a
/// layer, <see cref="RoomStage.ArchitectureRows"/> for one room): what <see cref="BackdropRowSpec"/> holds, nearest row
/// first in a list.
/// </summary>
[GlobalClass]
public partial class BackdropRowData : Resource
{
    /// <summary>How far behind the play plane the row stands, meters.</summary>
    [Export] public float Depth { get; set; } = 15f;

    /// <summary>The row's bay, meters.</summary>
    [Export] public float Pitch { get; set; } = 9f;

    /// <summary>The size of its pieces, 1 for the default nearest row's.</summary>
    [Export] public float Scale { get; set; } = 1f;

    public BackdropRowSpec ToCore() => new(Depth, Pitch, Scale);
}
