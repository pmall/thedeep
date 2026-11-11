using Godot;

namespace TheDeep;

/// <summary>
/// A body of water: its surface at the node's height, <see cref="Size"/> wide (X) and deep into the scene
/// (Z), <see cref="Depth"/> meters deep. Builds the surface mesh (told its floor and sides, so it draws its depth) and
/// the reflection probe it mirrors, and pushes its level, floor, area and murk into the water globals so nearby materials draw caustics
/// and sink into the water what lies in it (lib/water.gdshaderinc). One water body
/// per room (the globals hold one). Colors come from the biome. With <see cref="Swim"/>, the pilgrim swims in it:
/// its play-plane region (<see cref="Size"/>.X wide, from the surface down <see cref="Depth"/>) becomes an area on the water
/// layer that the pilgrim finds itself in (<see cref="Player.WaterSurface"/>), and the map's geometry check swims it.
/// With a <see cref="SectionMaterial"/>, its front on the play plane is drawn, so water seen from the side reads as water, and so is
/// the passage of each flooded seam it runs through (<see cref="Flood"/>).
/// </summary>
public partial class WaterBody : Node3D
{
    /// <summary>Where the water level sits when no water body is loaded: far below anything.</summary>
    private const float NoWaterLevel = -1e4f;

    /// <summary>The section shader's per-instance surface height.</summary>
    private static readonly StringName SurfaceParameter = "surface_y";

    /// <summary>The surface shader's per-instance floor height and sides, from which it draws the water's depth.</summary>
    private static readonly StringName FloorParameter = "floor_y";

    private static readonly StringName BoundsParameter = "bounds";

    /// <summary>
    /// The render layer of the water's own meshes: only they take the reflection probe's picture, and the probe leaves
    /// them out of it.
    /// </summary>
    private const uint WaterRenderLayer = 1u << 1;

    /// <summary>How high above the surface the reflection probe's box reaches, meters.</summary>
    private const float ReflectionHeight = 18f;

    /// <summary>How high above the surface the reflection probe takes its picture, meters.</summary>
    private const float ReflectionEye = 1.5f;

    /// <summary>Surface size: X across the room, Y into the scene (world Z).</summary>
    [Export] public Vector2 Size { get; set; } = new(10f, 10f);

    /// <summary>How far below the surface the water reaches, meters.</summary>
    [Export] public float Depth { get; set; } = 4f;

    /// <summary>
    /// How thick the water is: scales its murk and how fast the room's light fades in it (lib/water.gdshaderinc). 1 is
    /// the biome's water as it is; under 1 clearer (a room lit by its own lamps deep under the surface), over 1 murkier.
    /// </summary>
    [Export(PropertyHint.Range, "0.1,3,0.05")] public float Murk { get; set; } = 1f;

    /// <summary>
    /// How bright the room shows in the surface: the reflection probe's intensity. 1 is the room as lit; under 1 a
    /// dimmer mirror, for a room whose bright glow (a furnace, magma) would light the water up rather than sit in dark water.
    /// </summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float Reflection { get; set; } = 1f;

    /// <summary>Whether the pilgrim swims in it (its region on the play plane is water); otherwise it is scenery.</summary>
    [Export] public bool Swim { get; set; }

    /// <summary>
    /// Whether it pushes its level, floor, area and murk into the water globals (caustics, the murk). One water body per room does;
    /// a second one (a pool on the play plane beside scenery water at the same level) doesn't.
    /// </summary>
    [Export] public bool Globals { get; set; } = true;

    /// <summary>The body's front, seen from the side: the world Z of the cut (the blocks' front faces are at 2).</summary>
    [Export] public float SectionFront { get; set; } = 2.05f;

    /// <summary>
    /// The part of the front that is water, in the body's local X (across the room) and Y (up, the surface at 0): for
    /// scenery water that shows on the play plane only between blocks (a pit), so the front never veils a block's face.
    /// Empty: the whole front, <see cref="Size"/>.X wide and <see cref="Depth"/> deep.
    /// </summary>
    [Export] public Rect2 SectionArea { get; set; }

    [Export] public Material SurfaceMaterial { get; set; } = null!;

    /// <summary>
    /// The look of the body's front (<c>water_section.tres</c>): every swimmable body has one; scenery water has one
    /// where it reaches the play plane, none where it lies only behind it.
    /// </summary>
    [Export] public Material? SectionMaterial { get; set; }

    private static readonly System.Collections.Generic.List<WaterBody> Swimmable = new();

    /// <summary>The fronts of the flooded passages this water runs through past its own front (<see cref="Flood"/>), body-local.</summary>
    private readonly System.Collections.Generic.List<Rect2> _passages = new();

    /// <summary>
    /// The surface height of the swimmable water whose play-plane region holds (<paramref name="x"/>, <paramref name="y"/>),
    /// or null. Read from the bodies in the tree, so it allocates nothing (a physics point query returns engine dictionaries).
    /// </summary>
    public static float? SurfaceAt(float x, float y)
    {
        foreach (WaterBody water in Swimmable)
        {
            Box r = water.PlayRegion;
            if (x >= r.MinX && x <= r.MaxX && y >= r.MinY && y <= r.MaxY)
            {
                return r.MaxY;
            }
        }

        return null;
    }

    public override void _EnterTree()
    {
        if (Swim)
        {
            Swimmable.Add(this);
        }
    }

    /// <summary>The surface's height.</summary>
    public float SurfaceY => GlobalPosition.Y;

    /// <summary>The surface's extent in world X (rect X) and Z (rect Y).</summary>
    public Rect2 Area => new(GlobalPosition.X - 0.5f * Size.X, GlobalPosition.Z - 0.5f * Size.Y, Size);

    /// <summary>The water on the play plane (its top is the surface), for the map's geometry check.</summary>
    public Box PlayRegion => new(GlobalPosition.X - 0.5f * Size.X, SurfaceY - Depth, GlobalPosition.X + 0.5f * Size.X, SurfaceY);

    /// <summary>
    /// Runs this water through a flooded seam's passage (<see cref="SeamEnd.Flooded"/>; <see cref="SeamFrame.Passage"/>, room
    /// coordinates on the play plane): the pilgrim swims it, so the water must already fill it, and its front is drawn there too
    /// when the body's own front stops short of it (a pool kept off the wall over a doorway), so the passage reads as water.
    /// </summary>
    public void Flood(Box passage)
    {
        Box region = PlayRegion;
        if (!Swim || passage.MinX < region.MinX || passage.MaxX > region.MaxX || passage.MinY < region.MinY || passage.MaxY > region.MaxY)
        {
            throw new System.InvalidOperationException(
                $"{GetPath()} doesn't fill the flooded passage {passage}: it must be swum and reach the frame's outer face (docs/gameplay.md#seam-rule)");
        }

        var front = new Rect2(passage.MinX - GlobalPosition.X, passage.MinY - SurfaceY, passage.MaxX - passage.MinX, passage.MaxY - passage.MinY);
        if (SectionMaterial is null || DrawnFront.Encloses(front))
        {
            return;
        }

        _passages.Add(front);
        if (IsNodeReady())
        {
            AddSection(front, SectionMaterial);
        }
    }

    /// <summary>The part of the front drawn as water by the body itself, body-local.</summary>
    private Rect2 DrawnFront => SectionArea.HasArea() ? SectionArea : new Rect2(-0.5f * Size.X, -Depth, Size.X, Depth);

    /// <summary>Draws one part of the front, <paramref name="front"/> in the body's local X and Y.</summary>
    private void AddSection(Rect2 front, Material material)
    {
        var section = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = front.Size },
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Layers = WaterRenderLayer,
            Position = new Vector3(front.GetCenter().X, front.GetCenter().Y, SectionFront - GlobalPosition.Z),
        };
        section.SetInstanceShaderParameter(SurfaceParameter, SurfaceY);
        AddChild(section);
    }

    public override void _Ready()
    {
        if (Swim)
        {
            var area = new Area3D { CollisionLayer = PhysicsLayers.Water, CollisionMask = 0, Monitoring = false };
            using var box = new BoxShape3D { Size = new Vector3(Size.X, Depth, 1f) };
            area.AddChild(new CollisionShape3D { Shape = box });
            area.Position = new Vector3(0f, -0.5f * Depth, -GlobalPosition.Z);
            AddChild(area);
        }

        if (SectionMaterial is not null)
        {
            AddSection(DrawnFront, SectionMaterial);
            foreach (Rect2 passage in _passages)
            {
                AddSection(passage, SectionMaterial);
            }
        }

        Vector3 center = GlobalPosition;
        Rect2 extent = Area;
        var rect = new Vector4(extent.Position.X, extent.Position.Y, extent.End.X, extent.End.Y);
        var surface = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = Size },
            MaterialOverride = SurfaceMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Layers = WaterRenderLayer,
        };
        surface.SetInstanceShaderParameter(FloorParameter, SurfaceY - Depth);
        surface.SetInstanceShaderParameter(BoundsParameter, rect);
        AddChild(surface);
        AddChild(ReflectionOfRoom());
        if (!Globals)
        {
            return;
        }

        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.WaterLevel, center.Y);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.WaterFloor, center.Y - Depth);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.WaterMurk, Murk);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.WaterArea, rect);
    }

    /// <summary>
    /// What the surface reflects: the room above it, pictured once when the room loads (a fixed picture, so it never
    /// flickers), box-projected so it lines up with the walls, and shown on the water alone. Its sharpness is the
    /// reflection atlas's size in project.godot.
    /// </summary>
    private ReflectionProbe ReflectionOfRoom()
    {
        float eyeZ = Mathf.Clamp(-GlobalPosition.Z, -0.5f * Size.Y + 0.5f, 0.5f * Size.Y - 0.5f);
        return new ReflectionProbe
        {
            // From a meter under the surface, so the surface lies well inside the box, where no blending fades it.
            Size = new Vector3(Size.X, ReflectionHeight + 1f, Size.Y),
            Position = new Vector3(0f, 0.5f * (ReflectionHeight - 1f), 0f),
            OriginOffset = new Vector3(0f, ReflectionEye + 0.5f - 0.5f * ReflectionHeight, eyeZ),
            BlendDistance = 0f,
            Intensity = Reflection,
            BoxProjection = true,
            Interior = true,
            EnableShadows = true,
            AmbientMode = ReflectionProbe.AmbientModeEnum.Disabled,
            UpdateMode = ReflectionProbe.UpdateModeEnum.Once,
            CullMask = ~WaterRenderLayer & 0xFFFFF,
            ReflectionMask = WaterRenderLayer,
        };
    }

    public override void _ExitTree()
    {
        Swimmable.Remove(this);
        if (!Globals)
        {
            return;
        }

        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.WaterLevel, NoWaterLevel);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.WaterFloor, 2f * NoWaterLevel);
    }
}
