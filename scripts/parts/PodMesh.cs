using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// Part: a smooth pod (a limb, a head, a torso shell, a bulb) hanging from its origin along <see cref="Axis"/>.
/// Its outline is a list of <see cref="Stations"/>; <see cref="Curl"/> bends it into a claw, a horn or an
/// antenna. See <see cref="PartShapes.Pod"/>.
/// </summary>
[GlobalClass]
public partial class PodMesh : KitMesh
{
    private Vector3 _axis = Vector3.Down;
    private float _length = 0.4f;
    private Vector4[] _stations = { new(0f, 0.1f, 0.1f, 0f), new(1f, 0.08f, 0.08f, 0f) };
    private int _sides = 16;
    private int _rings = 12;
    private float _curl;

    [Export] public Vector3 Axis { get => _axis; set { _axis = value; RequestUpdate(); } }
    [Export] public float Length { get => _length; set { _length = value; RequestUpdate(); } }

    /// <summary>(t from 0 to 1 along the length, width along X, depth along Z, shift along X), meters. Zero width and depth round an end off.</summary>
    [Export] public Vector4[] Stations { get => _stations; set { _stations = value; RequestUpdate(); } }

    [Export] public int Sides { get => _sides; set { _sides = value; RequestUpdate(); } }
    [Export] public int Rings { get => _rings; set { _rings = value; RequestUpdate(); } }

    /// <summary>Bend counterclockwise around Z over the length, degrees (positive hooks a hanging pod forward).</summary>
    [Export] public float Curl { get => _curl; set { _curl = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder)
    {
        if (Stations.Length >= 2)
        {
            PartShapes.Pod(builder, ToNumerics(Axis), Length,
                Stations.Select(s => new System.Numerics.Vector4(s.X, s.Y, s.Z, s.W)).ToArray(), Sides, Rings, Mathf.DegToRad(Curl));
        }
    }
}
