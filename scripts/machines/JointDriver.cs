using Godot;

namespace TheDeep;

/// <summary>
/// One piece of a machine's procedural animation, as data: a joint posed, waved, spun or aimed, scaled by
/// a channel of its brain (bowing with Pray, a blade rising with Strike) or by its speed. A species lists
/// its drivers; several on one joint add up. See <see cref="MachineAnimator"/>.
/// </summary>
[GlobalClass]
public partial class JointDriver : Resource
{
    [Export] public StringName Joint { get; set; } = "";
    [Export] public DriverMode Mode { get; set; } = DriverMode.Pose;
    [Export] public DriverChannel Channel { get; set; } = DriverChannel.One;

    /// <summary>Follows 1 − channel instead: a sleeper folding as its Rouse falls, a body crouching when not alert.</summary>
    [Export] public bool Invert { get; set; }

    /// <summary>The weight when the channel is 0; it grows to 1 with the channel (a rotor idling at 0.2).</summary>
    [Export(PropertyHint.Range, "0,1")] public float Base { get; set; }

    /// <summary>Rotation axis in the joint's parent frame; Z turns in the side plane, as the camera sees it.</summary>
    [Export] public Vector3 Axis { get; set; } = Vector3.Back;

    /// <summary>Degrees: the pose's turn, the wave's amplitude, the spin's speed per second, the aim's limit.</summary>
    [Export] public float Angle { get; set; }

    /// <summary>Pose and Wave: how far the joint moves (meters) at full weight.</summary>
    [Export] public Vector3 Offset { get; set; }

    /// <summary>
    /// Pose on the Strike channel: the turn at the end of the wind-up (degrees), so a blade can draw back
    /// before it swings through to <see cref="Angle"/>. The recovery returns straight from the blow. 0: none.
    /// </summary>
    [Export] public float WindUp { get; set; }

    [ExportGroup("Wave")]
    [Export] public DriverClock Clock { get; set; } = DriverClock.Time;

    /// <summary>Waves per second (Time) or per meter walked (Gait).</summary>
    [Export] public float Rate { get; set; } = 1f;

    /// <summary>Where in its wave the driver starts, 0 to 1 (legs or fringes out of step).</summary>
    [Export] public float Phase { get; set; }

    /// <summary>Aim: how fast the joint turns toward its target, 1/s.</summary>
    [ExportGroup("Aim")]
    [Export] public float Sharpness { get; set; } = 6f;
}
