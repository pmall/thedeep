using Godot;

namespace TheDeep;

/// <summary>
/// A gameplay block: a box collider of <see cref="Size"/> whose <see cref="Surface"/> sets both its
/// physics layer (world_rock / world_metal, the source of truth for gameplay) and its default material.
/// <see cref="Material"/> overrides the look only (wet stone, rust…), never the physics layer.
/// Its mesh is a bevelled kit block; <see cref="CollisionOnly"/> hides it when kit pieces dress the
/// block instead (a grate, an arch), so collision stays a simple box under any visual.
/// </summary>
public partial class Block : StaticBody3D
{
    [Export] public Vector3 Size { get; set; } = Vector3.One;
    [Export] public SurfaceKind Surface { get; set; } = SurfaceKind.Rock;
    [Export] public Material RockMaterial { get; set; } = null!;
    [Export] public Material MetalMaterial { get; set; } = null!;

    /// <summary>Optional look replacing the default material of <see cref="Surface"/>.</summary>
    [Export] public Material? Material { get; set; }

    /// <summary>Chamfer on the mesh's edges, meters (visual only).</summary>
    [Export] public float Bevel { get; set; } = 0.05f;

    /// <summary>No mesh: the block is only collision, under kit pieces that show it.</summary>
    [Export] public bool CollisionOnly { get; set; }

    public override void _Ready()
    {
        bool metal = Surface == SurfaceKind.Metal;
        CollisionLayer = metal ? PhysicsLayers.WorldMetal : PhysicsLayers.WorldRock;
        CollisionMask = 0;

        var mesh = GetNode<MeshInstance3D>("%Mesh");
        if (CollisionOnly)
        {
            mesh.QueueFree();
        }
        else
        {
            mesh.Mesh = new BlockMesh { Size = Size, Bevel = Bevel };
            mesh.MaterialOverride = Material ?? (metal ? MetalMaterial : RockMaterial);
        }

        GetNode<CollisionShape3D>("%Shape").Shape = new BoxShape3D { Size = Size };
    }
}
