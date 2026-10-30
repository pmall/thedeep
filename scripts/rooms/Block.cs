using System;
using Godot;

namespace TheDeep;

/// <summary>
/// A gameplay block: a box collider of <see cref="Size"/> whose <see cref="Surface"/> sets both its
/// physics layer (world_rock / world_metal, the source of truth for gameplay) and its default material.
/// <see cref="Material"/> overrides the look only (wet stone, rust…), never the physics layer.
/// Its mesh is a bevelled kit block; <see cref="CollisionOnly"/> hides it when kit pieces dress the
/// block instead (a grate, an arch), so collision stays a simple box under any visual.
/// A <see cref="Cracked"/> block is rock the Drill bores through: it shatters (<see cref="Break"/>) and stays broken
/// (<see cref="Session"/> remembers it by room and name, and calls <see cref="Settle"/> on later visits).
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

    /// <summary>Cracked rock: the Drill breaks it. Always on the rock layer; its default look is <see cref="CrackedMaterial"/>.</summary>
    [Export] public bool Cracked { get; set; }

    [Export] public Material CrackedMaterial { get; set; } = null!;

    /// <summary>The dust thrown up when it shatters (a one-shot particle burst).</summary>
    [Export] public PackedScene? ShatterDust { get; set; }

    /// <summary>Broken by the Drill (or remembered broken): no collision, no mesh.</summary>
    public bool Broken { get; private set; }

    /// <summary>Raised when the Drill breaks it.</summary>
    public event Action<Block>? Shattered;

    private MeshInstance3D? _mesh;
    private CollisionShape3D _shape = null!;

    public override void _Ready()
    {
        bool metal = Surface == SurfaceKind.Metal && !Cracked;
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
            mesh.MaterialOverride = Material ?? (Cracked ? CrackedMaterial : metal ? MetalMaterial : RockMaterial);
            _mesh = mesh;
        }

        _shape = GetNode<CollisionShape3D>("%Shape");
        _shape.Shape = new BoxShape3D { Size = Size };
    }

    /// <summary>The Drill bores into it, moving along <paramref name="push"/>: it shatters, and the way is open.</summary>
    public void Break(Vector3 push)
    {
        if (!Cracked || Broken)
        {
            return;
        }

        Open();
        var shatter = new Shatter { Size = Size, Material = _mesh?.MaterialOverride, Push = push, Dust = ShatterDust };
        AddChild(shatter);
        Shattered?.Invoke(this);
    }

    /// <summary>Remembered broken: gone without a sound.</summary>
    public void Settle()
    {
        if (Cracked)
        {
            Open();
        }
    }

    private void Open()
    {
        Broken = true;
        // Off the layers at once (a dash boring through moves this very tick); the shape follows when physics allows.
        CollisionLayer = 0;
        _shape.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
        if (_mesh is not null)
        {
            _mesh.Visible = false;
        }
    }
}
