namespace TheDeep;

/// <summary>
/// What a surface pattern says about one point of its tile (<see cref="ISurfacePattern"/>), before it is packed into textures
/// (<see cref="SurfaceMaps"/>).
/// </summary>
/// <param name="HeightMeters">Relief out of the face, meters (only its slope matters: it becomes the normal map).</param>
/// <param name="Tone">The albedo multiplier, about 1; the material's palette color gives the hue.</param>
/// <param name="Cavity">1 deep in a joint or a crack, 0 on the open face: where silt settles and light doesn't reach.</param>
/// <param name="Roughness">The dry surface's roughness, 0 to 1.</param>
/// <param name="Cell">Which piece of the pattern the point belongs to (a block's index in its course), 0 to
/// <see cref="SurfaceMaps.Cells"/> - 1, so a material can give each piece its own look; 0 where the pattern has no pieces.</param>
public readonly record struct SurfacePoint(double HeightMeters, double Tone, double Cavity, double Roughness, int Cell = 0);
