using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// The Harpoon's mooring ring as pure geometry (Godot-free, unit tested), facing the camera (+Z), its middle at the origin: the iron
/// ring and the shackle it hangs by, the lit hoop set in it, and the sight's marks that close on it when it is the harpoon's target.
/// Kept hollow, so a ring never reads as a power-up's stone held in its rings. The kit's meshes (MooringRingMesh, HoopMesh,
/// SightMesh) expose them to scenes.
/// </summary>
public static class AnchorShapes
{
    /// <summary>
    /// The iron ring: a bar <paramref name="thickness"/> wide and a little deeper, round a circle of <paramref name="radius"/> (to the
    /// bar's middle); <paramref name="collars"/> collars clamped round it on the diagonals, and over its top a shackle, a link turned
    /// edge-on that it hangs by. The shackle's top is <see cref="ShackleTop"/> above the middle.
    /// </summary>
    public static void Ring(MeshBuilder b, float radius, float thickness, int collars)
    {
        b.Variation = 0f;
        b.Sweep(SweepPath.Arc(Vector2.Zero, radius, 0f, MathF.Tau, 40), Profile.Rect(thickness, thickness * 1.25f, thickness * 0.25f),
            capStart: false, capEnd: false, wrapsAlong: true);

        float half = MathF.Atan2(thickness * 0.9f, radius);
        for (int i = 0; i < collars; i++)
        {
            float a = MathF.PI * 0.25f + MathF.Tau * i / Math.Max(1, collars);
            b.Variation = 0.3f + 0.6f * i / Math.Max(1, collars);
            b.Sweep(SweepPath.Arc(Vector2.Zero, radius, a - half, a + half, 4), Profile.Rect(thickness * 1.55f, thickness * 1.7f, thickness * 0.3f));
        }

        // The shackle: a link in the plane across the camera's view, its foot through the ring's bar.
        float link = Link(thickness);
        Matrix4x4 placement = b.Transform;
        b.Transform = Matrix4x4.CreateRotationY(MathF.PI * 0.5f) * Matrix4x4.CreateTranslation(0f, radius + link * 0.55f, 0f) * placement;
        b.Variation = 0.7f;
        b.Sweep(SweepPath.Arc(Vector2.Zero, link, 0f, MathF.Tau, 16), Profile.Circle(thickness * 0.32f, 8),
            capStart: false, capEnd: false, wrapsAlong: true);
        b.Transform = placement;
        b.Variation = 0f;
    }

    /// <summary>How far above the ring's middle its shackle's top is: where its rod or cable starts.</summary>
    public static float ShackleTop(float radius, float thickness) => radius + Link(thickness) * 1.55f + thickness * 0.32f;

    /// <summary>
    /// The lit hoop: a flat band <paramref name="width"/> wide and <paramref name="depth"/> deep round a circle of <paramref name="radius"/>
    /// (to its middle), to be set inside the iron ring's bar.
    /// </summary>
    public static void Hoop(MeshBuilder b, float radius, float width, float depth)
    {
        b.Variation = 0f;
        b.Sweep(SweepPath.Arc(Vector2.Zero, radius, 0f, MathF.Tau, 48), Profile.Rect(width, depth, width * 0.2f),
            capStart: false, capEnd: false, wrapsAlong: true);
    }

    /// <summary>
    /// The sight: <paramref name="marks"/> wedges round the ring, the first at its top-right diagonal, each pointing at the middle with its
    /// tip <paramref name="radius"/> from it, <paramref name="size"/> long, <paramref name="depth"/> deep from the origin's plane toward the
    /// camera. Scaled about the middle, they close on the ring.
    /// </summary>
    public static void Sight(MeshBuilder b, float radius, int marks, float size, float depth)
    {
        marks = Math.Max(1, marks);
        for (int i = 0; i < marks; i++)
        {
            float a = MathF.PI * 0.25f + MathF.Tau * i / marks;
            var outward = new Vector2(MathF.Cos(a), MathF.Sin(a));
            var across = new Vector2(-outward.Y, outward.X);
            Vector2 tip = outward * radius, foot = outward * (radius + size);
            var wedge = new[] { tip, foot + across * (size * 0.55f), foot - across * (size * 0.55f) };
            b.Variation = (float)i / marks;
            b.Sweep(SweepPath.Line(Vector3.Zero, new Vector3(0f, 0f, depth), Vector3.UnitY), Profile.Polygon(wedge));
        }

        b.Variation = 0f;
    }

    private static float Link(float thickness) => thickness * 1.5f;
}
