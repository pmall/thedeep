using Godot;

namespace TheDeep;

/// <summary>
/// A body of water: its surface at the node's height, <see cref="Size"/> wide (X) and deep into the scene
/// (Z), with underwater haze <see cref="Depth"/> meters down. Builds the surface mesh and the haze volume,
/// and pushes its level and area into the water globals so nearby materials draw caustics. One water body
/// per room for now (the globals hold one). Colors come from the biome. With <see cref="Swim"/>, the pilgrim swims in it:
/// its play-plane region (<see cref="Size"/>.X wide, from the surface down <see cref="Depth"/>) becomes an area on the water
/// layer that the pilgrim finds itself in (<see cref="Player.WaterSurface"/>), and the map's geometry check swims it.
/// </summary>
public partial class WaterBody : Node3D
{
    /// <summary>Where the water level sits when no water body is loaded: far below anything.</summary>
    private const float NoWaterLevel = -1e4f;

    /// <summary>A swimmable body's haze stops this far behind the play plane (world Z), behind the blocks' back faces.</summary>
    private const float HazeFront = -2.5f;

    /// <summary>The section shader's per-instance surface height.</summary>
    private static readonly StringName SurfaceParameter = "surface_y";

    /// <summary>Surface size: X across the room, Y into the scene (world Z).</summary>
    [Export] public Vector2 Size { get; set; } = new(10f, 10f);

    /// <summary>How far below the surface the underwater haze reaches, meters.</summary>
    [Export] public float Depth { get; set; } = 4f;

    /// <summary>Whether the pilgrim swims in it (its region on the play plane is water); otherwise it is scenery.</summary>
    [Export] public bool Swim { get; set; }

    /// <summary>
    /// Whether it pushes its level and area into the water globals (caustics, the haze's level). One water body per room does;
    /// a second one (a pool on the play plane beside scenery water at the same level) doesn't.
    /// </summary>
    [Export] public bool Globals { get; set; } = true;

    /// <summary>A swimmable body's front, seen from the side: the world Z of the cut (the blocks' front faces are at 2).</summary>
    [Export] public float SectionFront { get; set; } = 2.05f;

    [Export] public Material SurfaceMaterial { get; set; } = null!;
    [Export] public Material UnderwaterMaterial { get; set; } = null!;

    /// <summary>The look of a swimmable body's front (<c>water_section.tres</c>); none for scenery water.</summary>
    [Export] public Material? SectionMaterial { get; set; }

    private static readonly System.Collections.Generic.List<WaterBody> Swimmable = new();

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

    /// <summary>The water on the play plane (its top is the surface), for the map's geometry check.</summary>
    public Box PlayRegion => new(GlobalPosition.X - 0.5f * Size.X, SurfaceY - Depth, GlobalPosition.X + 0.5f * Size.X, SurfaceY);

    public override void _Ready()
    {
        if (Swim)
        {
            var area = new Area3D { CollisionLayer = PhysicsLayers.Water, CollisionMask = 0, Monitoring = false };
            area.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(Size.X, Depth, 1f) } });
            area.Position = new Vector3(0f, -0.5f * Depth, -GlobalPosition.Z);
            AddChild(area);
            if (SectionMaterial is not null)
            {
                var section = new MeshInstance3D
                {
                    Mesh = new QuadMesh { Size = new Vector2(Size.X, Depth) },
                    MaterialOverride = SectionMaterial,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    Position = new Vector3(0f, -0.5f * Depth, SectionFront - GlobalPosition.Z),
                };
                section.SetInstanceShaderParameter(SurfaceParameter, SurfaceY);
                AddChild(section);
            }
        }

        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = Size },
            MaterialOverride = SurfaceMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        // The haze fills the water behind the play plane; a swimmable body keeps it off the play plane and in front of it,
        // so what is swum through stays readable (its section tints it instead).
        float back = GlobalPosition.Z - 0.5f * Size.Y, front = GlobalPosition.Z + 0.5f * Size.Y;
        if (Swim)
        {
            front = Mathf.Min(front, HazeFront);
        }

        if (front > back)
        {
            AddChild(new FogVolume
            {
                Size = new Vector3(Size.X, Depth, front - back),
                Shape = RenderingServer.FogVolumeShape.Box,
                Material = UnderwaterMaterial,
                Position = new Vector3(0f, -0.5f * Depth, 0.5f * (back + front) - GlobalPosition.Z),
            });
        }

        if (!Globals)
        {
            return;
        }

        Vector3 center = GlobalPosition;
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.WaterLevel, center.Y);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.WaterArea, new Vector4(
            center.X - 0.5f * Size.X, center.Z - 0.5f * Size.Y, center.X + 0.5f * Size.X, center.Z + 0.5f * Size.Y));
    }

    public override void _ExitTree()
    {
        Swimmable.Remove(this);
        if (!Globals)
        {
            return;
        }

        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.WaterLevel, NoWaterLevel);
    }
}
