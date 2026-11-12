namespace TheDeep;

/// <summary>
/// A standpipe hung from a flooded hatch in a roof (<see cref="SeamFrame.Pipe"/>), on the play plane: the column of water from the
/// slab's top down into the room's own water, so the way through the hatch runs on into a room whose water lies lower, and the two
/// walls that hold it, hung from the slab's underside. <paramref name="Pool"/>: which of the room's waters it stands in.
/// </summary>
public sealed record Standpipe(int Pool, Box Column, Box West, Box East);
