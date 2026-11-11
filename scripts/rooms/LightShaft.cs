using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// The visible beam of one of a room's light shafts (<see cref="Room.Shafts"/>): a soft column of light from the spot light down to
/// the solid it falls on, as wide as its cone, in the biome's shaft color, as bright as the light's light_volumetric_fog_energy says (<see cref="ShaftBeam"/> builds it, its
/// <see cref="Material"/> draws it). Built once by the room's stage (<see cref="RoomStage.Build"/>) for every shaft that is a spot
/// light with a fog energy above 0, so a room only places its lights and picks which of them show a beam. Visual only.
/// </summary>
public partial class LightShaft : MeshInstance3D
{
    /// <summary>How far past the cone the strip reaches, for the halo around it (the cone's rim at 1 / this across).</summary>
    private const float Spread = 1.6f;

    /// <summary>The deepest the beam is drawn (Z), just behind the play plane's solids and in front of the rooms' back walls.</summary>
    private const float Deepest = -3.5f;

    private static readonly StringName PowerParameter = "power";

    private static readonly StringName FocusParameter = "focus";

    public required SpotLight3D Light { get; init; }

    /// <summary>The beam's look (assets/materials/light_shaft.tres).</summary>
    public required Material Material { get; init; }

    /// <summary>The room's solids (<see cref="Room.SolidBoxes"/>): the beam ends on the first it meets.</summary>
    public required IReadOnlyList<SolidBox> Solids { get; init; }

    /// <summary>The bottom of the room's view (<see cref="Room.BoundsMin"/>): a beam sinking under it ends there.</summary>
    public required float FloorY { get; init; }

    public override void _Ready()
    {
        Transform3D light = Light.GlobalTransform;
        System.Numerics.Vector3 origin = light.Origin.ToCore();
        System.Numerics.Vector3 direction = (-light.Basis.Z).ToCore();
        float length = ShaftBeam.Reach(origin, direction, Light.SpotRange, FloorY, Solids);
        BeamStrip strip = ShaftBeam.Build(
            origin, direction, Mathf.DegToRad(Light.SpotAngle), length, Spread, System.Numerics.Vector3.UnitZ, Deepest);

        // The node stands at the light, so the beam's breath takes its phase from where the light is.
        GlobalTransform = new Transform3D(Basis.Identity, light.Origin);

        int count = strip.Positions.Length;
        var vertices = new Vector3[count];
        var uvs = new Vector2[count];
        var shape = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            vertices[i] = strip.Positions[i].ToGodot();
            uvs[i] = strip.Uvs[i].ToGodot();
            shape[i] = strip.Shape.ToGodot();
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.TexUV2] = shape;
        arrays[(int)Mesh.ArrayType.Index] = strip.Indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        Mesh = mesh;
        MaterialOverride = Material;
        CastShadow = ShadowCastingSetting.Off;
        SetInstanceShaderParameter(PowerParameter, Light.LightVolumetricFogEnergy);
        SetInstanceShaderParameter(FocusParameter, Light.SpotAngleAttenuation);
    }
}
