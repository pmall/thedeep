using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>Pieces a check lays in the mechanics lab, as real as the lab's own.</summary>
public static class LabProps
{
    private static readonly StringName TankName = "Tank";

    /// <summary>A block of the kit: named, centered at <paramref name="center"/>, <paramref name="size"/> wide, high and deep.</summary>
    public static Block Block(string name, Vector3 center, Vector3 size, SurfaceKind surface = SurfaceKind.Rock, bool cracked = false)
    {
        var block = GD.Load<PackedScene>("res://scenes/kit/Block.tscn").Instantiate<Block>();
        block.Named(name);
        block.Surface = surface;
        block.Size = size;
        block.Cracked = cracked;
        block.Position = center;
        return block;
    }

    /// <summary>A tank of swimmable water named "Tank": its <paramref name="size"/> across and deep in the plane, its
    /// <paramref name="depth"/> down from the surface, its surface at <paramref name="position"/>.</summary>
    public static WaterBody Tank(Vector2 size, float depth, Vector3 position) => new()
    {
        Name = TankName,
        Swim = true,
        Size = size,
        Depth = depth,
        Position = position,
        SurfaceMaterial = GD.Load<Material>("res://assets/materials/water.tres"),
    };
}

/// <summary>The gates the pilgrim has said it can't pass, as <see cref="GateHints"/> speaks them.</summary>
public sealed class HintLog
{
    private readonly HashSet<Ability> _said = new();

    /// <summary>Listens to <paramref name="hints"/>.</summary>
    public HintLog(GateHints hints) => hints.Said += kind => _said.Add(kind);

    /// <summary>Whether the pilgrim has said it can't pass the gate of <paramref name="kind"/>.</summary>
    public bool Said(Ability kind) => _said.Contains(kind);
}
