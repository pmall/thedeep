using Godot;

namespace TheDeep;

/// <summary>
/// A doorway's frame (a <see cref="SeamEnd"/>): on a <see cref="RoomExit"/> in a west or east wall, the sill under the opening and the
/// lintel over it, so the room's walls and floor stop where the frame starts; and the wall it is cut in, over the lintel and under the
/// sill (<see cref="SeamFrame.Wall"/>).
/// </summary>
public partial class Doorway : SeamEnd
{
    /// <summary>Whether the wall rises over the lintel; false where the room's route runs over it, which then ends plainly.</summary>
    [Export] public bool Head { get; set; } = true;

    /// <summary>Whether the wall drops under the sill; false where the room's route runs under it, which a bracket then carries.</summary>
    [Export] public bool Foot { get; set; } = true;

    protected override bool FramesHatch => false;

    protected override string[] PieceNames { get; } = { "Sill", "Lintel" };

    protected override (bool Head, bool Foot) Walled => (Head, Foot);
}
