namespace TheDeep;

/// <summary>
/// A point's place in a Voronoi diagram that tiles (<see cref="TileNoise.Voronoi"/>), all in lattice cells.
/// </summary>
/// <param name="Border">How far the point is from its cell's nearest border.</param>
/// <param name="Cell">Which cell, 0 to cells squared - 1: the same cell in every repeat of the tile.</param>
/// <param name="ToSiteX">From the point to its cell's site, along.</param>
/// <param name="ToSiteY">From the point to its cell's site, up.</param>
public readonly record struct VoronoiCell(double Border, int Cell, double ToSiteX, double ToSiteY);
