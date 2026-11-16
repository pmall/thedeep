
namespace TheDeep;

/// <summary>
/// One piece of a seam frame's trim (<see cref="SeamFrame.Trim"/>), visual only: its <paramref name="Face"/> on the play plane, relative
/// to the exit's marker, standing <paramref name="Proud"/> meters out of the frame's front face; or, <paramref name="Through"/>, a
/// liner over a face running through the frame's whole depth. <paramref name="Lining"/>: metal, where a grip climb runs.
/// <paramref name="Sink"/>: how far behind the front face it starts, meters.
/// </summary>
public readonly record struct TrimPiece(Box Face, float Proud, bool Lining, bool Through = false, float Sink = 0.05f);
