using System.Numerics;

namespace TheDeep;

/// <summary>
/// A beam's mesh (<see cref="ShaftBeam.Build"/>): two vertices per row, the triangles between them; <see cref="Shape"/> is every
/// vertex's UV2 (the length, the spread).
/// </summary>
public sealed record BeamStrip(Vector3[] Positions, Vector2[] Uvs, Vector2 Shape, int[] Indices);
