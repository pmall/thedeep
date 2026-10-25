using System;
using System.Numerics;

namespace TheDeep;

/// <summary>A pose on a surface: where the feet are and which way is up (the surface's normal).</summary>
public readonly record struct GripPose(Vector2 Feet, Vector2 Up);

/// <summary>
/// The geometry of mag-grip, pure. Gameplay blocks are axis-aligned boxes, so every surface normal is one
/// of ±X, ±Y; the player stands on a surface with its up along the normal, its local +X (its "forward"
/// when facing +1) along <see cref="Tangent"/>. Corners snap (docs/gameplay.md): an inner corner (a wall
/// ahead) turns the player onto that wall, an outer corner (the surface ends) wraps it around the edge.
/// Positions are on the play plane; the feet are the body's origin.
/// </summary>
public static class GripMath
{
    /// <summary>The surface's normal snapped to the nearest axis.</summary>
    public static Vector2 Snap(Vector2 normal) =>
        MathF.Abs(normal.X) >= MathF.Abs(normal.Y) ? new Vector2(MathF.Sign(normal.X), 0f) : new Vector2(0f, MathF.Sign(normal.Y));

    /// <summary>The body's local +X when its up is <paramref name="up"/> (a rotation about the camera axis).</summary>
    public static Vector2 Tangent(Vector2 up) => new(up.Y, -up.X);

    /// <summary>The rotation about +Z (radians) that turns world up into <paramref name="up"/>.</summary>
    public static float Angle(Vector2 up) => MathF.Atan2(-up.X, up.Y);

    /// <summary>
    /// The speed along the tangent that held input asks for, −1 to 1: the input's component along the
    /// surface. On a wall that is up/down, on a ceiling left/right (in screen terms, so pressing right
    /// always moves right).
    /// </summary>
    public static float Along(Vector2 input, Vector2 up) => Vector2.Dot(input, Tangent(up));

    /// <summary>Whether held input pulls away from the surface (letting go).</summary>
    public static bool PullsAway(Vector2 input, Vector2 up) => Vector2.Dot(input, up) > 0.5f;

    /// <summary>
    /// Moving along a surface into a wall ahead (normal <paramref name="wallUp"/>, its face through
    /// <paramref name="wallPoint"/>): the player turns onto that wall, its feet just clear of the old surface.
    /// </summary>
    public static GripPose InnerCorner(Vector2 feet, Vector2 up, Vector2 wallPoint, Vector2 wallUp, float clearance)
    {
        // Two perpendicular axis-aligned normals: the corner is where both faces' planes meet.
        Vector2 corner = up * Vector2.Dot(feet, up) + wallUp * Vector2.Dot(wallPoint, wallUp);
        return new GripPose(corner + up * clearance, wallUp);
    }

    /// <summary>
    /// Moving along a surface past its end, the end face found at <paramref name="edgeHit"/> (a point on it
    /// just under the surface): the player wraps around the edge onto that face, whose normal is the way it
    /// was moving (<paramref name="travel"/>), its feet just clear of the old surface's plane.
    /// </summary>
    public static GripPose OuterCorner(Vector2 feet, Vector2 up, Vector2 travel, Vector2 edgeHit, float clearance)
    {
        Vector2 newUp = Snap(travel);
        Vector2 corner = up * Vector2.Dot(feet, up) + newUp * Vector2.Dot(edgeHit, newUp);
        return new GripPose(corner - up * clearance, newUp);
    }

    /// <summary>After an inner corner the player keeps moving away from the old surface; after an outer one, back past its plane.</summary>
    public static Vector2 TravelAfterInner(Vector2 oldUp) => oldUp;

    public static Vector2 TravelAfterOuter(Vector2 oldUp) => -oldUp;

    /// <summary>
    /// Attaching to a wall from upright: the body turns about its center (so it swings out from the wall
    /// rather than into it), its feet landing on the wall's face at <paramref name="wallX"/>.
    /// </summary>
    public static GripPose OntoWall(Vector2 feet, float centerHeight, float wallX, Vector2 wallUp) =>
        new(new Vector2(wallX, feet.Y + centerHeight), wallUp);

    /// <summary>Attaching to a ceiling at <paramref name="ceilingY"/> from upright: hanging under it, head down.</summary>
    public static GripPose OntoCeiling(Vector2 feet, float ceilingY) => new(new Vector2(feet.X, ceilingY), new Vector2(0f, -1f));

    /// <summary>Letting go: upright again, turned about the body's center so the body stays where it was.</summary>
    public static Vector2 Upright(Vector2 feet, Vector2 up, float centerHeight) => feet + up * centerHeight - new Vector2(0f, centerHeight);
}
