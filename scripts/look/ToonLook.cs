using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// A cartoon look laid over a 3D figure, kept or dropped as one piece: each of the figure's materials drawn by
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

        if (Poses is not null)
        {
            Poses.PoseHoldSeconds = _on ? PoseHoldSeconds : 0f;
        }
    }
}
