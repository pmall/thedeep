using System.Collections.Generic;
using Godot;
using NQuaternion = System.Numerics.Quaternion;
using NVector2 = System.Numerics.Vector2;
using NVector3 = System.Numerics.Vector3;

namespace TheDeep;

/// <summary>
/// Builds a <see cref="PartAssembly"/> under itself: a Node3D per joint (named after it), the rigid parts on
/// them, and the flowing chains. Behavior code (the pilgrim's animation, later the machines') poses the
/// joints on physics ticks, so physics interpolation keeps rigid parts smooth, then calls
/// <see cref="StepChains"/>. Chains are simulated on physics ticks too and drawn every frame between the
/// last two ticks, so they stay smooth at any refresh rate.
/// </summary>
public partial class PartBody : Node3D
{
    /// <summary>Ribbon chains: a flat strip with softened edges.</summary>
    private const float RibbonBevelShare = 0.4f;

    private const int TubeSides = 8;

    [Export] public PartAssembly Assembly { get; set; } = null!;

    private readonly Dictionary<StringName, Node3D> _joints = new();
    private readonly Dictionary<StringName, Vector3> _rest = new();
    private readonly List<Chain> _chains = new();

    public override void _Ready()
    {
        Build();
    }

    /// <summary>The joint named <paramref name="name"/>, or null.</summary>
    public Node3D? Joint(StringName name) => _joints.GetValueOrDefault(name);

    /// <summary>The joint's rest position on its parent.</summary>
    public Vector3 RestOffset(StringName name) => _rest.GetValueOrDefault(name);

    /// <summary>The node at the end of the chain named <paramref name="name"/> (its pendant), or null.</summary>
    public Node3D? ChainEnd(StringName name)
    {
        foreach (Chain chain in _chains)
        {
            if (chain.Spec.Name == name)
            {
                return chain.End;
            }
        }

        return null;
    }

    /// <summary>Advances every chain by one physics tick, after the joints are posed. <paramref name="floorY"/>: NaN for none.</summary>
    public void StepChains(float delta, float floorY)
    {
        foreach (Chain chain in _chains)
        {
            System.Array.Copy(chain.Sim.Points, chain.Previous, chain.Previous.Length);
            chain.Sim.Floor = chain.Spec.StaysAboveFloor ? floorY : float.NaN;
            (NVector3 anchor, NQuaternion rotation) = Anchor(chain);
            chain.Sim.Step(delta, anchor, rotation);
        }
    }

    /// <summary>Puts every chain at rest under its joint (spawn, teleport).</summary>
    public void ResetChains()
    {
        foreach (Chain chain in _chains)
        {
            (NVector3 anchor, NQuaternion rotation) = Anchor(chain);
            chain.Sim.Reset(anchor, rotation);
            System.Array.Copy(chain.Sim.Points, chain.Previous, chain.Previous.Length);
        }
    }

    public override void _Process(double delta)
    {
        float t = (float)Engine.GetPhysicsInterpolationFraction();
        foreach (Chain chain in _chains)
        {
            DrawChain(chain, t);
        }
    }

    private void Build()
    {
        foreach (RigJoint spec in Assembly.Joints)
        {
            var joint = new Node3D { Name = spec.Name, Position = spec.Offset };
            Node3D parent = this;
            if (!spec.Parent.IsEmpty && !_joints.TryGetValue(spec.Parent, out parent!))
            {
                GD.PushError($"{Assembly.ResourcePath}: joint {spec.Name} hangs from unknown joint {spec.Parent} (list parents first)");
                parent = this;
            }

            parent.AddChild(joint);
            _joints[spec.Name] = joint;
            _rest[spec.Name] = spec.Offset;
        }

        foreach (PartSlot slot in Assembly.Parts)
        {
            if (Joint(slot.Joint) is not { } joint)
            {
                GD.PushError($"{Assembly.ResourcePath}: part on unknown joint {slot.Joint}");
                continue;
            }

            Vector3 radians = slot.Rotation * (Mathf.Pi / 180f);
            joint.AddChild(new MeshInstance3D
            {
                Mesh = slot.Mesh,
                MaterialOverride = slot.Material,
                Transform = new Transform3D(Basis.FromEuler(radians).Scaled(slot.Scale), slot.Position),
            });
        }

        foreach (PartChain spec in Assembly.Chains)
        {
            if (Joint(spec.Joint) is not { } joint)
            {
                GD.PushError($"{Assembly.ResourcePath}: chain {spec.Name} on unknown joint {spec.Joint}");
                continue;
            }

            var sim = new VerletChain(spec.Links, spec.Length / Mathf.Max(1, spec.Links))
            {
                Gravity = spec.Gravity,
                Drag = spec.Drag,
                Stiffness = spec.Stiffness,
                RestDirection = ToNumerics(spec.RestDirection),
                RestCurl = Mathf.DegToRad(spec.RestCurl),
            };

            // Drawn in world space every frame: top level, and not interpolated by the engine.
            var skin = new MeshInstance3D
            {
                Mesh = new ArrayMesh(), MaterialOverride = spec.Material, TopLevel = true,
                PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off,
            };
            AddChild(skin);
            var end = new Node3D { TopLevel = true, PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off };
            AddChild(end);
            if (spec.Pendant is not null)
            {
                end.AddChild(new MeshInstance3D { Mesh = spec.Pendant, MaterialOverride = spec.PendantMaterial });
            }

            _chains.Add(new Chain(spec, joint, sim, skin, end, new NVector3[sim.Points.Length]));
        }

        ResetChains();
    }

    private static (NVector3 Anchor, NQuaternion Rotation) Anchor(Chain chain)
    {
        Transform3D t = chain.Joint.GlobalTransform;
        Quaternion q = t.Basis.Orthonormalized().GetRotationQuaternion();
        return (ToNumerics(t * chain.Spec.Offset), new NQuaternion(q.X, q.Y, q.Z, q.W));
    }

    private void DrawChain(Chain chain, float t)
    {
        NVector3[] current = chain.Sim.Points;
        int count = current.Length;
        var points = new NVector3[count];
        for (int i = 0; i < count; i++)
        {
            points[i] = NVector3.Lerp(chain.Previous[i], current[i], t);
        }

        // The chain's broad face (a ribbon's) looks along the joint's Z axis, toward the camera when facing right.
        Vector3 faceAxis = chain.Joint.GetGlobalTransformInterpolated().Basis.Z.Normalized();
        PartChain spec = chain.Spec;
        var scales = new NVector2[count];
        for (int i = 0; i < count; i++)
        {
            float k = 1f + (spec.Taper - 1f) * i / (count - 1);
            scales[i] = new NVector2(k, spec.Shape == ChainShape.Ribbon ? 1f : k);
        }

        var builder = new MeshBuilder();
        Profile profile = spec.Shape == ChainShape.Ribbon
            ? Profile.Rect(spec.Width, spec.Thickness, spec.Thickness * RibbonBevelShare)
            : Profile.Circle(spec.Width * 0.5f, TubeSides);
        List<PathFrame> path = SweepPath.FromPoints(points, ToNumerics(faceAxis));
        builder.Sweep(path, profile, scales: scales);
        var mesh = (ArrayMesh)chain.Skin.Mesh;
        mesh.ClearSurfaces();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, KitMesh.ToArrays(builder.Finish()));

        // The pendant hangs along the last link.
        NVector3 tip = points[^1];
        NVector3 up = NVector3.Normalize(points[^2] - tip);
        Vector3 y = new(up.X, up.Y, up.Z);
        Vector3 z = (faceAxis - y * faceAxis.Dot(y)).Normalized();
        if (z.LengthSquared() < 1e-6f)
        {
            z = Vector3.Back;
        }

        chain.End.GlobalTransform = new Transform3D(new Basis(y.Cross(z), y, z), new Vector3(tip.X, tip.Y, tip.Z));
    }

    private static NVector3 ToNumerics(Vector3 v) => new(v.X, v.Y, v.Z);

    private sealed record Chain(PartChain Spec, Node3D Joint, VerletChain Sim, MeshInstance3D Skin, Node3D End, NVector3[] Previous);
}
