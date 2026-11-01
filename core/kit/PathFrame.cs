using System.Numerics;

namespace TheDeep;

/// <summary>
/// One station along a swept path: where the profile sits and how it is oriented.
/// The profile's X runs along <see cref="Side"/>, its Y along <see cref="Up"/>.
/// </summary>
/// <param name="Tangent">Unit direction of travel.</param>
/// <param name="Side">Unit axis for the profile's X, perpendicular to the tangent (normals use it).</param>
/// <param name="Up">Unit axis for the profile's Y, perpendicular to the tangent and the side.</param>
/// <param name="Distance">Distance along the path from its start, meters (the U of the surface frame).</param>
/// <param name="SideOffset">
/// What the profile's X is multiplied by to place points. Equal to <see cref="Side"/> except where the
/// cut is mitred (the apex of a pointed arch), where it is stretched to land on the mitre plane.
/// </param>
public readonly record struct PathFrame(Vector3 Position, Vector3 Tangent, Vector3 Side, Vector3 Up, float Distance, Vector3 SideOffset)
{
    public PathFrame(Vector3 position, Vector3 tangent, Vector3 side, Vector3 up, float distance)
        : this(position, tangent, side, up, distance, side)
    {
    }

    /// <summary>Where profile point <paramref name="p"/> lands in space.</summary>
    public Vector3 Place(Vector2 p) => Position + SideOffset * p.X + Up * p.Y;

    /// <summary>A profile direction (a 2D normal) in space.</summary>
    public Vector3 Direction(Vector2 d) => Vector3.Normalize(Side * d.X + Up * d.Y);
}
