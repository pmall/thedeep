using System;
using System.Collections.Generic;
using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// Poses a machine's <see cref="PartBody"/> each physics tick from its species' <see cref="JointDriver"/>s
/// and <see cref="MachineLeg"/>s: drivers first (bowing, waving, spinning, aiming), then the legs by IK
/// against the ground, in the frame of <c>ground</c> (the node carrying the body: X forward, Y up).
/// </summary>
public sealed class MachineAnimator
{
    private readonly PartBody _body;
    private readonly Node3D _ground;
    private readonly JointDriver[] _drivers;
    private readonly float[] _driverState;
    private readonly Dictionary<StringName, Node3D> _driven = new();
    private readonly List<Leg> _legs = new();

    public MachineAnimator(PartBody body, Node3D ground, MachineSpecies species)
    {
        _body = body;
        _ground = ground;
        _drivers = species.Drivers;
        _driverState = new float[_drivers.Length];
        foreach (JointDriver driver in _drivers)
        {
            if (body.Joint(driver.Joint) is { } joint)
            {
                _driven[driver.Joint] = joint;
            }
            else
            {
                GD.PushError($"{species.ResourcePath}: driver on unknown joint {driver.Joint}");
            }
        }

        // Rest positions in the body's frame, from the rig's offsets (no joint is turned at rest).
        var rest = new Dictionary<StringName, Vector3>();
        foreach (RigJoint joint in species.Assembly.Joints)
        {
            rest[joint.Name] = (joint.Parent.IsEmpty ? Vector3.Zero : rest.GetValueOrDefault(joint.Parent)) + joint.Offset;
        }

        foreach (MachineLeg spec in species.Legs)
        {
            if (body.Joint(spec.Upper) is not { } upper || body.Joint(spec.Lower) is not { } lower || body.Joint(spec.Foot) is null)
            {
                GD.PushError($"{species.ResourcePath}: leg {spec.Upper}/{spec.Lower}/{spec.Foot} has an unknown joint");
                continue;
            }

            Vector3 knee = body.RestOffset(spec.Lower), foot = body.RestOffset(spec.Foot);
            _legs.Add(new Leg(spec, upper, lower, Flat(knee).Length(), Flat(foot).Length(), RestAngle(knee), RestAngle(foot),
                Flat(rest.GetValueOrDefault(spec.Foot))));
        }
    }

    /// <summary>
    /// Poses the body. <paramref name="intent"/> carries the brain's channels and gaze; <paramref name="speed"/>
    /// is 0 to 1 of the species' top speed; <paramref name="walked"/> the meters walked so far;
    /// <paramref name="stepping"/> how much the legs step (0 standing, 1 walking).
    /// </summary>
    public void Update(float delta, float time, in Intent intent, float speed, float walked, float stepping)
    {
        foreach ((StringName name, Node3D joint) in _driven)
        {
            joint.Transform = new Transform3D(Basis.Identity, _body.RestOffset(name));
        }

        for (int i = 0; i < _drivers.Length; i++)
        {
            JointDriver d = _drivers[i];
            if (!_driven.TryGetValue(d.Joint, out Node3D? joint))
            {
                continue;
            }

            float channel = d.Channel switch
            {
                DriverChannel.Speed => speed,
                DriverChannel.Pray => intent.Pray,
                DriverChannel.Strike => intent.Strike,
                DriverChannel.Alert => intent.Alert,
                DriverChannel.Rouse => intent.Rouse,
                DriverChannel.Work => intent.Work,
                _ => 1f,
            };
            if (d.Invert)
            {
                channel = 1f - channel;
            }

            float weight = d.Base + (1f - d.Base) * channel;
            Vector3 axis = d.Axis.LengthSquared() > 1e-8f ? d.Axis.Normalized() : Vector3.Back;
            float angle = 0f;
            Vector3 offset = Vector3.Zero;
            switch (d.Mode)
            {
                case DriverMode.Pose when d.Channel == DriverChannel.Strike && d.WindUp != 0f && intent.Act != MachineAct.Recover:
                    // Draw back over the wind-up (0 to 0.6), swing through on the blow (0.6 to 1).
                    angle = channel <= 0.6f ? d.WindUp * channel / 0.6f : Mathf.Lerp(d.WindUp, d.Angle, (channel - 0.6f) / 0.4f);
                    offset = d.Offset * channel;
                    break;
                case DriverMode.Pose:
                    angle = d.Angle * weight;
                    offset = d.Offset * weight;
                    break;
                case DriverMode.Wave:
                    float clock = d.Clock == DriverClock.Gait ? walked : time;
                    float wave = Mathf.Sin(Mathf.Tau * (d.Rate * clock + d.Phase));
                    angle = d.Angle * weight * wave;
                    offset = d.Offset * weight * wave;
                    break;
                case DriverMode.Spin:
                    _driverState[i] = Mathf.PosMod(_driverState[i] + d.Angle * weight * delta, 360f);
                    angle = _driverState[i];
                    break;
                case DriverMode.Aim:
                    float target = intent.LookAt is { } look ? AimAngle(joint, look, d.Angle) : 0f;
                    _driverState[i] = Motion.Damp(_driverState[i], target * weight, d.Sharpness, delta);
                    angle = _driverState[i];
                    break;
            }

            Transform3D t = joint.Transform;
            joint.Transform = new Transform3D(t.Basis * new Basis(axis, Mathf.DegToRad(angle)), t.Origin + offset);
        }

        foreach (Leg leg in _legs)
        {
            Step(leg, walked, stepping);
        }
    }

    private void Step(Leg leg, float walked, float stepping)
    {
        MachineLeg spec = leg.Spec;
        float phase = LegGait.Advance(walked, spec.Stride, spec.Stance) + spec.Phase;
        NVector2 target = leg.RestFoot + LegGait.Foot(phase, spec.Stance, spec.Stride, spec.Lift) * stepping;
        Transform3D toGround = _ground.GlobalTransform.AffineInverse();
        NVector2 hip = Flat(toGround * leg.Upper.GlobalPosition);
        (float upper, float lower) = PlanarIk.TwoBone(hip, target, leg.UpperLength, leg.LowerLength, spec.Bend);

        // Absolute angles in the ground frame, to local turns: minus the parent's turn and the segment's rest angle.
        Vector3 parentX = toGround.Basis * ((Node3D)leg.Upper.GetParent()).GlobalBasis.X;
        float parentTurn = Mathf.Atan2(parentX.Y, parentX.X);
        float upperTurn = upper - leg.UpperRest - parentTurn;
        leg.Upper.Transform = new Transform3D(new Basis(Vector3.Back, upperTurn), _body.RestOffset(spec.Upper));
        float lowerTurn = lower - leg.LowerRest - (parentTurn + upperTurn);
        leg.Lower.Transform = new Transform3D(new Basis(Vector3.Back, lowerTurn), _body.RestOffset(spec.Lower));
    }

    /// <summary>The turn around Z (degrees, within ±<paramref name="limit"/>) that points the joint's forward at <paramref name="look"/>.</summary>
    private static float AimAngle(Node3D joint, System.Numerics.Vector2 look, float limit)
    {
        Node3D parent = (Node3D)joint.GetParent();
        Vector3 local = parent.GlobalTransform.AffineInverse() * new Vector3(look.X, look.Y, 0f) - joint.Position;
        float angle = Mathf.RadToDeg(Mathf.Atan2(local.Y, local.X));
        return Mathf.Clamp(angle, -limit, limit);
    }

    /// <summary>The angle of a segment from straight down, positive toward +X (PlanarIk's convention).</summary>
    private static float RestAngle(Vector3 offset) => Mathf.Atan2(offset.X, -offset.Y);

    private static NVector2 Flat(Vector3 v) => new(v.X, v.Y);

    private sealed record Leg(MachineLeg Spec, Node3D Upper, Node3D Lower, float UpperLength, float LowerLength,
        float UpperRest, float LowerRest, NVector2 RestFoot);
}
