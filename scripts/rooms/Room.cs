using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>Root of a room scene: where the player spawns, the area the camera may show, and its look.</summary>
public partial class Room : Node3D
{
    [Export] public Marker3D SpawnPoint { get; set; } = null!;

    /// <summary>The depth layer's look, applied on ready (see <see cref="ApplyBiome"/>).</summary>
    [Export] public BiomeProfile Biome { get; set; } = null!;

    /// <summary>The shared atmosphere stack (scenes/stage/RoomStage.tscn): environment, key light, backdrop, frame, dust.</summary>
    [Export] public RoomStage Stage { get; set; } = null!;

    /// <summary>Lamps that take the biome's lamp color.</summary>
    [Export] public Light3D[] Lamps { get; set; } = System.Array.Empty<Light3D>();

    /// <summary>Light shafts (spot lights seen in the volumetric fog) that take the biome's shaft color.</summary>
    [Export] public Light3D[] Shafts { get; set; } = System.Array.Empty<Light3D>();

    /// <summary>Small lights at emissive decor (faith glyphs) that take the biome's glyph color.</summary>
    [Export] public Light3D[] GlyphLights { get; set; } = System.Array.Empty<Light3D>();

    /// <summary>False for rooms that are not part of the game (the machine arena, the mechanics lab): the map solver leaves them out.</summary>
    [Export] public bool InGame { get; set; } = true;

    /// <summary>Lower-left corner (X, Y) of the area the camera is allowed to show, on the play plane.</summary>
    [Export] public Vector2 BoundsMin { get; set; } = new(-10f, -10f);

    /// <summary>Upper-right corner (X, Y) of the area the camera is allowed to show, on the play plane.</summary>
    [Export] public Vector2 BoundsMax { get; set; } = new(10f, 10f);

    public override void _Ready()
    {
        Stage.Build(this);
        ApplyBiome(Biome);
    }

    /// <summary>Re-themes the room with <paramref name="biome"/>; can be called at any time.</summary>
    public void ApplyBiome(BiomeProfile biome)
    {
        Biome = biome;
        Stage.Restyle(biome);
        biome.Apply(Stage.Environment.Environment, Stage.Paint, Stage.KeyLight, Lamps, Shafts, GlyphLights);
    }

    /// <summary>The room's look-dev camera spots (tools/capture.sh), in scene order.</summary>
    public IEnumerable<PhotoSpot> PhotoSpots() => Find<PhotoSpot>(this);

    /// <summary>The room's connection markers, in scene order.</summary>
    public IEnumerable<RoomExit> Exits() => Find<RoomExit>(this);

    /// <summary>The exit named <paramref name="name"/>, or null.</summary>
    public RoomExit? FindExit(string name)
    {
        foreach (RoomExit exit in Exits())
        {
            if (exit.Name == name)
            {
                return exit;
            }
        }

        return null;
    }

    /// <summary>The room's gameplay blocks, in scene order.</summary>
    public IEnumerable<Block> Blocks() => Find<Block>(this);

    /// <summary>
    /// Every node of type <typeparamref name="T"/> in the room, in scene order (shrines, relics…). Found once and kept until a node
    /// enters or leaves the room: the session asks every physics tick, and walking the room's tree each time allocated about
    /// 250 KB a frame (a garbage collection every few frames, each one a dropped frame).
    /// </summary>
    public IReadOnlyList<T> All<T>() where T : Node
    {
        if (_found.TryGetValue(typeof(T), out object? kept))
        {
            return (List<T>)kept;
        }

        var found = new List<T>(Find<T>(this));
        _found[typeof(T)] = found;
        return found;
    }

    private readonly Dictionary<System.Type, object> _found = new();

    public override void _EnterTree()
    {
        _found.Clear();
        GetTree().NodeAdded += Changed;
        GetTree().NodeRemoved += Changed;
    }

    public override void _ExitTree()
    {
        GetTree().NodeAdded -= Changed;
        GetTree().NodeRemoved -= Changed;
    }

    private void Changed(Node node)
    {
        if (_found.Count > 0 && IsAncestorOf(node))
        {
            _found.Clear();
        }
    }

    private static IEnumerable<T> Find<T>(Node parent) where T : Node
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is T match)
            {
                yield return match;
            }

            foreach (T nested in Find<T>(child))
            {
                yield return nested;
            }
        }
    }
}
