namespace TheDeep;

/// <summary>
/// A doorway's frame (a <see cref="SeamEnd"/>): on a <see cref="RoomExit"/> in a west or east wall, the sill under the opening and the
/// lintel over it, so the room's walls and floor stop where the frame starts.
/// </summary>
public partial class Doorway : SeamEnd
{
    protected override bool FramesHatch => false;

    protected override string[] PieceNames { get; } = { "Sill", "Lintel" };
}
