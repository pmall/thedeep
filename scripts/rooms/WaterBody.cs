using Godot;

namespace TheDeep;

/// <summary>
/// A body of water: its surface at the node's height, <see cref="Size"/> wide (X) and deep into the scene
/// (Z), with underwater haze <see cref="Depth"/> meters down. Builds the surface mesh and the haze volume,
/// and pushes its level and area into the water globals so nearby materials draw caustics. One water body
/// per room for now (the globals hold one). Colors come from the biome. Visual only: swimming and
/// Ballast will add a water-layer area (PhysicsLayers.Water) here.
/// </summary>
public partial class WaterBody : Node3D
{
    /// <summary>Where the water level sits when no water body is loaded: far below anything.</summary>
    private const float NoWaterLevel = -1e4f;

    /// <summary>Surface size: X across the room, Y into the scene (world Z).</summary>
    [Export] public Vector2 Size { get; set; } = new(10f, 10f);

    /// <summary>How far below the surface the underwater haze reaches, meters.</summary>
    [Export] public float Depth { get; set; } = 4f;

    [Export] public Material SurfaceMaterial { get; set; } = null!;
    [Export] public Material UnderwaterMaterial { get; set; } = null!;

    public override void _Ready()
    {
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = Size },
            MaterialOverride = SurfaceMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        AddChild(new FogVolume
        {
            Size = new Vector3(Size.X, Depth, Size.Y),
            Shape = RenderingServer.FogVolumeShape.Box,
            Material = UnderwaterMaterial,
            Position = new Vector3(0f, -0.5f * Depth, 0f),
        });

        Vector3 center = GlobalPosition;
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.WaterLevel, center.Y);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.WaterArea, new Vector4(
            center.X - 0.5f * Size.X, center.Z - 0.5f * Size.Y, center.X + 0.5f * Size.X, center.Z + 0.5f * Size.Y));
    }

    public override void _ExitTree()
    {
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.WaterLevel, NoWaterLevel);
    }
}
