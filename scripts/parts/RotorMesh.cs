using Godot;

namespace TheDeep;

/// <summary>Part: a rotor spinning around Y (a hub and pitched blades); see <see cref="PartShapes.Rotor"/>.</summary>
[GlobalClass]
public partial class RotorMesh : KitMesh
{
    private float _radius = 0.4f;
    private int _blades = 3;
    private float _chord = 0.09f;
    private float _pitch = 12f;
    private float _hubRadius = 0.05f;
    private float _hubHeight = 0.05f;

    [Export] public float Radius { get => _radius; set { _radius = value; RequestUpdate(); } }
    [Export] public int Blades { get => _blades; set { _blades = value; RequestUpdate(); } }

    /// <summary>Blade width at the root, meters.</summary>
    [Export] public float Chord { get => _chord; set { _chord = value; RequestUpdate(); } }

    /// <summary>Blade pitch, degrees.</summary>
    [Export] public float Pitch { get => _pitch; set { _pitch = value; RequestUpdate(); } }

    [Export] public float HubRadius { get => _hubRadius; set { _hubRadius = value; RequestUpdate(); } }
    [Export] public float HubHeight { get => _hubHeight; set { _hubHeight = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) =>
        PartShapes.Rotor(builder, Radius, Blades, Chord, Mathf.DegToRad(Pitch), HubRadius, HubHeight);
}
