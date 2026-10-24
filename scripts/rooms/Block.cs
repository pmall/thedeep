using Godot;

namespace TheDeep;

/// <summary>
/// A greybox world block: a box of <see cref="Size"/> whose <see cref="Surface"/> sets both its
/// physics layer (world_rock / world_metal, the source of truth for gameplay) and its default material.
/// <see cref="Material"/> overrides the look only (wet stone, rust…), never the physics layer.
/// </summary>
public partial class Block : StaticBody3D
{
    [Export] public Vector3 Size { get; set; } = Vector3.One;
    [Export] public SurfaceKind Surface { get; set; } = SurfaceKind.Rock;
    [Export] public Material RockMaterial { get; set; } = null!;
    [Export] public Material MetalMaterial { get; set; } = null!;

    /// <summary>Optional look replacing the default material of <see cref="Surface"/>.</summary>
    [Export] public Material? Material { get; set; }

    public override void _Ready()
    {
        bool metal = Surface == SurfaceKind.Metal;
        CollisionLayer = metal ? PhysicsLayers.WorldMetal : PhysicsLayers.WorldRock;
        CollisionMask = 0;

        var mesh = GetNode<MeshInstance3D>("%Mesh");
        mesh.Mesh = new BoxMesh { Size = Size };
        mesh.MaterialOverride = Material ?? (metal ? MetalMaterial : RockMaterial);
        mesh.SetInstanceShaderParameter(ShaderInstance.BoxSize, Size);

        GetNode<CollisionShape3D>("%Shape").Shape = new BoxShape3D { Size = Size };
    }
}
