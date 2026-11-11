namespace TheDeep;

/// <summary>
/// A hatch's frame (a <see cref="SeamEnd"/>): on a <see cref="RoomExit"/> in a floor or ceiling, the slab either side of the hole, as
/// thick as the exit's area, so the room's own floor or ceiling stops where the frame starts. What lies past the slab is each room's
/// own: each bounds its camera at the slab's far face, so the seam check sees only the frame.
/// </summary>
public partial class Hatch : SeamEnd
{
    protected override bool FramesHatch => true;

    protected override string[] PieceNames { get; } = { "SlabWest", "SlabEast" };
}
