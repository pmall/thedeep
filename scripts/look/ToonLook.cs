using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// The drawn look, kept or dropped as one piece. The world and the figure share its ink and paper grain (global
/// uniforms read by lib/look.gdshaderinc: <see cref="WorldInk"/>, <see cref="InkPixels"/>, <see cref="InkColor"/>,
/// <see cref="Grain"/>), so the figure is drawn like the place it walks in; the world is dirtied by lib/dirt (<see cref="Wash"/>,
/// <see cref="Drips"/>, <see cref="Hatch"/>), each part 0 to drop it. Over the figure: each of its materials drawn by
/// <see cref="From"/> is shown by a twin under <see cref="To"/> (the same finish and knobs, lit in flat bands:
/// android_toon.gdshader), with <see cref="Ink"/> as its next pass (an outline), and the figure's pose held on twos
/// (<see cref="PoseHoldSeconds"/>, through <see cref="Poses"/>). <see cref="On"/> switches it at any time; off, the figure is
/// exactly as it was, its own materials never touched. Debug builds switch it with F9; `--toon` starts with it.
/// </summary>
public partial class ToonLook : Node
{
    /// <summary>The figure whose meshes are dressed (every <see cref="MeshInstance3D"/> under it).</summary>
    [Export] public Node3D Figure { get; set; } = null!;

    /// <summary>The body whose shown pose is held while the look is on, or none to change only the materials.</summary>
    [Export] public PlayerVisual? Poses { get; set; }

    /// <summary>The shader the figure's own materials use, which the look replaces.</summary>
    [Export] public Shader From { get; set; } = null!;

    /// <summary>The shader that draws the same finishes as a cartoon (it declares the same parameters).</summary>
    [Export] public Shader To { get; set; } = null!;

    /// <summary>The outline drawn after each dressed material, or none.</summary>
    [Export] public Material? Ink { get; set; }

    /// <summary>How strong the ink along the world's edges is, 0–1.</summary>
    [Export(PropertyHint.Range, "0,1")] public float WorldInk { get; set; } = 0.8f;

    /// <summary>The ink line's width, pixels: the world's edges and the figure's outline.</summary>
    [Export] public float InkPixels { get; set; } = 1.5f;

    /// <summary>The ink's color (linear: the global uniform arrives unconverted).</summary>
    [Export] public Color InkColor { get; set; } = new(0.018f, 0.01f, 0.032f);

    /// <summary>How strong the paper grain is on the world and the figure, 0–1.</summary>
    [Export(PropertyHint.Range, "0,1")] public float Grain { get; set; } = 0.6f;

    /// <summary>How strong the watercolor stains on the world are, 0–1 (lib/dirt; 0 drops them).</summary>
    [Export(PropertyHint.Range, "0,1")] public float Wash { get; set; } = 1f;

    /// <summary>How strong the drips down the world's side faces are, 0–1 (lib/dirt; 0 drops them).</summary>
    [Export(PropertyHint.Range, "0,1")] public float Drips { get; set; } = 1f;

    /// <summary>How strong the ink hatching in the world's dark and dirty places is, 0–1 (lib/dirt; 0 drops it).</summary>
    [Export(PropertyHint.Range, "0,1")] public float Hatch { get; set; } = 1f;

    /// <summary>How long each shown pose holds while the look is on, seconds (0: smooth, as without it).</summary>
    [Export] public float PoseHoldSeconds { get; set; } = 1f / 15f;

    /// <summary>Whether the look is shown.</summary>
    [Export]
    public bool On
    {
        get => _on;
        set
        {
            _on = value;
            if (IsInsideTree())
            {
                Show();
            }
        }
    }

    private bool _on;

    /// <summary>One dressed surface: its own override (usually none) and the twin shown instead.</summary>
    private readonly record struct Dressed(MeshInstance3D Mesh, int Surface, Material? Own, ShaderMaterial Twin);

    private readonly List<Dressed> _dressed = new();

    public override void _Ready()
    {
        foreach (MeshInstance3D mesh in Figure.Descendants<MeshInstance3D>())
        {
            for (int i = 0; i < mesh.GetSurfaceOverrideMaterialCount(); i++)
            {
                Material? own = mesh.GetSurfaceOverrideMaterial(i);
                if ((own ?? mesh.Mesh.SurfaceGetMaterial(i)) is ShaderMaterial shown && shown.Shader == From)
                {
                    var twin = (ShaderMaterial)shown.Duplicate();
                    twin.Shader = To;
                    twin.NextPass = Ink;
                    _dressed.Add(new Dressed(mesh, i, own, twin));
                }
            }
        }

        Show();
    }

    /// <summary>Switches the look on or off.</summary>
    public void Toggle() => On = !On;

    private void Show()
    {
        foreach (Dressed d in _dressed)
        {
            d.Mesh.SetSurfaceOverrideMaterial(d.Surface, _on ? d.Twin : d.Own);
        }

        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.LookInk, _on ? WorldInk : 0f);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.LookGrain, _on ? Grain : 0f);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.LookWash, _on ? Wash : 0f);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.LookDrips, _on ? Drips : 0f);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.LookHatch, _on ? Hatch : 0f);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.LookInkPixels, InkPixels);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.LookInkColor, InkColor);
        if (Poses is not null)
        {
            Poses.PoseHoldSeconds = _on ? PoseHoldSeconds : 0f;
        }
    }
}
