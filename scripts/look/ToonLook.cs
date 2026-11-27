using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// The drawn look, kept or dropped as one piece, in several directions at once: each a <see cref="LookPreset"/> in
/// res://resources/looks/, one shown at a time (<see cref="Current"/>, none for the plain look). The world and the figure
/// share its ink and paper grain (global uniforms read by lib/look.gdshaderinc), the world is dirtied by lib/dirt, and a
/// preset may grade the whole frame (<see cref="LookGrade"/>, through the room's environment: <see cref="StyleRoom"/>) and
/// light the world in flat bands (<see cref="Banded"/>). Over
/// the figure: each of its materials drawn by <see cref="From"/> is shown by a twin under <see cref="To"/> (the same finish
/// and knobs, lit in flat bands: android_toon.gdshader) that takes the preset's drawing values, with an outline as its
/// next pass, and the figure's pose held on twos (through <see cref="Poses"/>). Plain, the figure is exactly as it was,
/// its own materials never touched. Debug builds cycle the presets with F9; `--look &lt;name&gt;` starts with one.
/// </summary>
public partial class ToonLook : Node
{
    /// <summary>The figure whose meshes are dressed (every <see cref="MeshInstance3D"/> under it).</summary>
    [Export] public Node3D Figure { get; set; } = null!;

    /// <summary>The body whose shown pose is held while a look is on, or none to change only the materials.</summary>
    [Export] public PlayerVisual? Poses { get; set; }

    /// <summary>The shader the figure's own materials use, which the look replaces.</summary>
    [Export] public Shader From { get; set; } = null!;

    /// <summary>The shader that draws the same finishes as a cartoon (it declares the same parameters).</summary>
    [Export] public Shader To { get; set; } = null!;

    /// <summary>The outline drawn after each dressed material (its width is the preset's: <see cref="LookPreset.FigureInk"/>).</summary>
    [Export] public ShaderMaterial Ink { get; set; } = null!;

    /// <summary>The world's lit shaders a look may band (<see cref="LookPreset.WorldBands"/>): each gets a twin with
    /// lib/bands' light() appended, swapped onto every material of a room that uses it while such a look is shown.</summary>
    [Export] public Shader[] Banded { get; set; } = System.Array.Empty<Shader>();

    /// <summary>The look shown, or null for the plain one.</summary>
    public LookPreset? Current { get; private set; }

    /// <summary>Every look there is, in the order F9 cycles them.</summary>
    public LookPreset[] Presets => _presets ??= LookPreset.All();

    private LookPreset[]? _presets;

    /// <summary>One dressed surface: its own override (usually none) and the twin shown instead.</summary>
    private readonly record struct Dressed(MeshInstance3D Mesh, int Surface, Material? Own, ShaderMaterial Twin);

    private readonly List<Dressed> _dressed = new();

    private ShaderMaterial? _ink;

    private readonly LookGrade _grade = new();

    /// <summary>Each banded shader's twin (<see cref="Banded"/>), made when first needed.</summary>
    private readonly Dictionary<Shader, Shader> _bandTwins = new();

    /// <summary>Every world material whose shader a look swapped, with the shader it had.</summary>
    private readonly Dictionary<ShaderMaterial, Shader> _banded = new();

    private const string BandsInclude = "\n#include \"res://assets/shaders/lib/bands.gdshaderinc\"\n";

    /// <summary>The room whose environment carries the grade, once one is given.</summary>
    private Room? _room;

    private static readonly StringName WidthShare = "width_share";
    private static readonly StringName ToonThreshold = "toon_threshold";
    private static readonly StringName ToonSoftness = "toon_softness";
    private static readonly StringName ToonShade = "toon_shade";
    private static readonly StringName ToonGloss = "toon_gloss";
    private static readonly StringName ToonRim = "toon_rim";
    private static readonly StringName ToonLift = "toon_lift";
    private static readonly StringName ToonDust = "toon_dust";
    private static readonly StringName ToonChips = "toon_chips";
    private static readonly StringName ToonEdge = "toon_edge";
    private static readonly StringName ToonScratch = "toon_scratch";
    private static readonly StringName ToonInkBody = "toon_ink_body";
    private static readonly StringName ToonWorldBands = "toon_world_bands";

    public override void _Ready()
    {
        _ink = (ShaderMaterial)Ink.Duplicate();
        foreach (MeshInstance3D mesh in Figure.Descendants<MeshInstance3D>())
        {
            for (int i = 0; i < mesh.GetSurfaceOverrideMaterialCount(); i++)
            {
                Material? own = mesh.GetSurfaceOverrideMaterial(i);
                if ((own ?? mesh.Mesh.SurfaceGetMaterial(i)) is ShaderMaterial shown && shown.Shader == From)
                {
                    var twin = (ShaderMaterial)shown.Duplicate();
                    twin.Shader = To;
                    twin.NextPass = _ink;
                    _dressed.Add(new Dressed(mesh, i, own, twin));
                }
            }
        }

        Show();
    }

    public override void _ExitTree() => _grade.Dispose();

    /// <summary>Shows the look named <paramref name="name"/>; false, changing nothing, if there is none.</summary>
    public bool Choose(string name)
    {
        foreach (LookPreset preset in Presets)
        {
            if (preset.Name == name)
            {
                Current = preset;
                Show();
                return true;
            }
        }

        return false;
    }

    /// <summary>Shows the next look: the plain one, then each preset in turn.</summary>
    public void Cycle()
    {
        int at = Current is null ? -1 : System.Array.IndexOf(Presets, Current);
        Current = at + 1 < Presets.Length ? Presets[at + 1] : null;
        Show();
    }

    /// <summary>Grades <paramref name="room"/>'s environment for the look shown and clears its vignette under a drawn look;
    /// called whenever a room is loaded or re-themed, since its biome sets its own grade and vignette.</summary>
    public void StyleRoom(Room room)
    {
        _room = room;
        if (!IsInstanceValid(room))
        {
            return;
        }

        // Nothing on the screen's edges in a drawn look (owner): the biome's vignette is cleared while one is shown.
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.Vignette, Current is null ? room.Biome.Vignette : 0f);
        Band(room, Current?.WorldBands ?? false);
        Environment environment = room.Stage.Environment.Environment;
        environment.AmbientLightEnergy = room.Biome.AmbientEnergy * (Current?.Ambient ?? 1f);
        if (Current?.Tone is { } tone)
        {
            environment.AdjustmentEnabled = true;
            environment.AdjustmentColorCorrection = _grade.For(room.Biome.Lift, room.Biome.Gain, tone);
        }
        else
        {
            room.Biome.Grade(environment);
        }
    }

    private void Show()
    {
        LookPreset? look = Current;
        _ink?.SetShaderParameter(WidthShare, look?.FigureInk ?? 0f);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.LookInk, look?.WorldInk ?? 0f);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.LookGrain, look?.Grain ?? 0f);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.LookWash, look?.Wash ?? 0f);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.LookDrips, look?.Drips ?? 0f);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.LookHatch, look?.Hatch ?? 0f);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.LookBrush, look?.Brush ?? 0f);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.LookMark, look?.Mark ?? 0f);
        if (look is { } preset)
        {
            foreach (Dressed d in _dressed)
            {
                d.Mesh.SetSurfaceOverrideMaterial(d.Surface, d.Twin);
                Dress(d.Twin, preset);
            }

            RenderingServer.GlobalShaderParameterSet(ShaderGlobals.LookMarkKind, (float)preset.MarkKind);
            RenderingServer.GlobalShaderParameterSet(ShaderGlobals.LookMarkCell, preset.MarkCell);
            RenderingServer.GlobalShaderParameterSet(ShaderGlobals.LookMarkGain, preset.MarkGain);
            RenderingServer.GlobalShaderParameterSet(ShaderGlobals.LookBandSteps, preset.BandSteps);
            RenderingServer.GlobalShaderParameterSet(ShaderGlobals.LookBandSoftness, preset.BandSoftness);
            RenderingServer.GlobalShaderParameterSet(ShaderGlobals.LookBandShade, preset.BandShade);
            RenderingServer.GlobalShaderParameterSet(ShaderGlobals.LookBandGloss, preset.BandGloss);
            RenderingServer.GlobalShaderParameterSet(ShaderGlobals.LookInkPixels, preset.InkPixels);
            RenderingServer.GlobalShaderParameterSet(ShaderGlobals.LookInkColor, preset.InkColor);
        }
        else
        {
            foreach (Dressed d in _dressed)
            {
                d.Mesh.SetSurfaceOverrideMaterial(d.Surface, d.Own);
            }
        }

        if (Poses is not null)
        {
            Poses.PoseHoldSeconds = look?.PoseHoldSeconds ?? 0f;
        }

        if (_room is not null)
        {
            StyleRoom(_room);
        }
    }

    /// <summary>Swaps every banded shader in <paramref name="room"/> for its twin when <paramref name="on"/>, and every
    /// material swapped before back to its own shader when not. The materials are shared resources, so a swap holds for
    /// every room that uses them until it is undone.</summary>
    private void Band(Room room, bool on)
    {
        if (!on)
        {
            foreach ((ShaderMaterial material, Shader own) in _banded)
            {
                material.Shader = own;
            }

            _banded.Clear();
            return;
        }

        foreach (GeometryInstance3D geometry in room.Descendants<GeometryInstance3D>())
        {
            BandMaterial(geometry.MaterialOverride);
            if (geometry is MeshInstance3D { Mesh: { } mesh } instance)
            {
                for (int i = 0; i < mesh.GetSurfaceCount(); i++)
                {
                    BandMaterial(instance.GetSurfaceOverrideMaterial(i));
                    BandMaterial(mesh.SurfaceGetMaterial(i));
                }
            }
        }
    }

    private void BandMaterial(Material? material)
    {
        if (material is not ShaderMaterial { Shader: { } shader } shaded || _banded.ContainsKey(shaded)
            || System.Array.IndexOf(Banded, shader) < 0)
        {
            return;
        }

        if (!_bandTwins.TryGetValue(shader, out Shader? twin))
        {
            twin = new Shader { Code = shader.Code + BandsInclude };
            _bandTwins[shader] = twin;
        }

        _banded[shaded] = shader;
        shaded.Shader = twin;
    }

    private static void Dress(ShaderMaterial twin, LookPreset look)
    {
        twin.SetShaderParameter(ToonThreshold, look.Threshold);
        twin.SetShaderParameter(ToonSoftness, look.Softness);
        twin.SetShaderParameter(ToonShade, look.Shade);
        twin.SetShaderParameter(ToonGloss, look.Gloss);
        twin.SetShaderParameter(ToonRim, look.Rim);
        twin.SetShaderParameter(ToonLift, look.Lift);
        twin.SetShaderParameter(ToonDust, look.Dust);
        twin.SetShaderParameter(ToonChips, look.Chips);
        twin.SetShaderParameter(ToonEdge, look.Edge);
        twin.SetShaderParameter(ToonScratch, look.Scratch);
        twin.SetShaderParameter(ToonInkBody, look.InkBody);
        twin.SetShaderParameter(ToonWorldBands, look.WorldBands ? 1f : 0f);
    }
}
