using System;
using System.Collections.Generic;
using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// Scripted Mag-grip check (and, first, the ledge assist) in the mechanics lab (`-- --room MechanicsLab --grip-check`, run by tools/check.sh),
/// through the real input actions: without Mag-grip, a full jump at a rock step a little higher than a jump lands on its top (the ledge
/// assist); press into a metal wall and stick to it, climb it and turn under the slab
/// above (inner corner), wrap around the slab's end and onto its top (two outer corners, then upright);
/// jump into a metal block's underside and hang from it, walk out and wrap up onto its top; leap off a
/// wall; let go of a wall by pulling away. Then, beside pieces it places in the lab: a pier whose metal foot goes on up as rock
/// (climbing past the metal lets go: only metal holds) and a tank of water at the metal wall's foot (walking down into it lets
/// go, and the pilgrim swims). Each step has a time limit. Prints `GRIP OK` or `GRIP CHECK` errors.
/// </summary>
public partial class GripCheck : StepDriver
{
    /// <summary>The pier east of the metal wall: its east face, the top of its metal foot, its top.</summary>
    private const float PierFace = 30f, PierMetalTop = 4f, PierTop = 10f;

    /// <summary>The rock step west of the metal block: its east face and its top, higher than a full jump.</summary>
    private const float StepFace = 2.5f, StepTop = 3.6f;

    /// <summary>The tank between the metal wall and the pier: its surface (its floor is the lab's).</summary>
    private const float TankSurface = 5f;

    private readonly HashSet<NVector2> _upsSeen = new();
    private float _maxAbsZ;
    private float _highestGrip = float.NegativeInfinity;
    private float _lowestGrip = float.PositiveInfinity;

    protected override void Build()
    {
        foreach ((string name, float x0, float x1, float bottom, float top, SurfaceKind surface) in new[]
                 {
                     ("Step", 0f, StepFace, 0f, StepTop, SurfaceKind.Rock),
                     ("PierFoot", PierFace - 2f, PierFace, 0f, PierMetalTop, SurfaceKind.Metal),
                     ("Pier", PierFace - 2f, PierFace, PierMetalTop, PierTop, SurfaceKind.Rock),
                 })
        {
            World.Room.AddChild(LabProps.Block(name, new Vector3(0.5f * (x0 + x1), 0.5f * (bottom + top), 0f),
                new Vector3(x1 - x0, top - bottom, 4f), surface));
        }

        World.Room.AddChild(LabProps.Tank(new Vector2(2f, 4f), TankSurface, new Vector3(27f, TankSurface, 0f)));

        NVector2 left = new(-1f, 0f), right = new(1f, 0f), down = new(0f, -1f);
        Steps.Add(new("a full jump at a step just higher than a jump lands on its top (the ledge assist)", 2f,
            () => { Player.Abilities.Lock(Ability.MagGrip); Place(StepFace + 1.5f, 0f); Press(InputActions.MoveLeft); Press(InputActions.Jump); },
            () => Player.IsUpright && Player.IsOnFloor() && Player.GlobalPosition.Y > StepTop - 0.05f));
        Steps.Add(new("stick to the metal wall when pressing into it", 2f,
            () => { Release(InputActions.MoveLeft); Release(InputActions.Jump); Place(20f, 0f); Player.Abilities.Unlock(Ability.MagGrip); Press(InputActions.MoveRight); },
            () => Player.CurrentState == Player.States.Grip && Player.SurfaceUp == left));
        Steps.Add(new("climb the wall, turn under the slab, wrap its end and stand on its top", 9f,
            () => { Release(InputActions.MoveRight); Press(InputActions.MoveUp); },
            () => Player.IsUpright && Player.IsOnFloor() && Player.GlobalPosition.Y > 12.9f));
        Steps.Add(new("seen on the way: the wall, the slab's underside, its end face", 0f, () => { }, () =>
            _upsSeen.Contains(left) && _upsSeen.Contains(down) && _upsSeen.Contains(new NVector2(-1f, 0f))));
        Steps.Add(new("jump into the block's underside and hang from it", 2f,
            () => { Release(InputActions.MoveUp); Place(8f, 0f); Press(InputActions.Jump); },
            () => Player.CurrentState == Player.States.Grip && Player.SurfaceUp == down));
        Steps.Add(new("walk out under the block, wrap its end and stand on its top", 5f,
            () => { Release(InputActions.Jump); Press(InputActions.MoveRight); },
            () => Player.IsUpright && Player.IsOnFloor() && Mathf.Abs(Player.GlobalPosition.Y - 6.5f) < 0.05f));
        Steps.Add(new("stick to the wall again", 2f,
            () => { Place(20f, 0f); Press(InputActions.MoveRight); },
            () => Player.CurrentState == Player.States.Grip));
        Steps.Add(new("leap off the wall, away from it and upright", 1f,
            () => { Release(InputActions.MoveRight); Press(InputActions.Jump); },
            () => Player.IsUpright && Player.Velocity.X < -3f && Player.Velocity.Y > 0f));
        Steps.Add(new("stick to the wall once more", 2.5f,
            () => { Release(InputActions.Jump); Place(20f, 0f); Press(InputActions.MoveRight); },
            () => Player.CurrentState == Player.States.Grip));
        Steps.Add(new("let go by pulling away from the wall", 1f,
            () => { Release(InputActions.MoveRight); Press(InputActions.MoveLeft); },
            () => Player.IsUpright && Player.CurrentState != Player.States.Grip));
        Steps.Add(new("stick to the pier's metal foot", 2f,
            () => { Release(InputActions.MoveLeft); _highestGrip = float.NegativeInfinity; Place(PierFace + 1f, 0f); Press(InputActions.MoveLeft); },
            () => Player.CurrentState == Player.States.Grip && Player.SurfaceUp == right));
        Steps.Add(new("climb past the metal onto the rock above: it lets go", 3f,
            () => { Release(InputActions.MoveLeft); Press(InputActions.MoveUp); },
            () => Player.IsUpright && Player.CurrentState != Player.States.Grip));
        Steps.Add(new("let go below the rock (never held by it)", 0f, () => { },
            () => _highestGrip < PierMetalTop + 1f));
        Steps.Add(new("grip the metal wall above the tank", 2.5f,
            () => { Release(InputActions.MoveUp); _lowestGrip = float.PositiveInfinity; Place(27f, 8f); Press(InputActions.MoveLeft); },
            () => Player.CurrentState == Player.States.Grip && Player.SurfaceUp == right));
        Steps.Add(new("walk down into the water: it lets go, and the pilgrim swims", 3f,
            () => { Release(InputActions.MoveLeft); Press(InputActions.MoveDown); },
            () => Player.CurrentState == Player.States.Swim));
        Steps.Add(new("never held deep under the water", 0f, () => { },
            () => _lowestGrip > TankSurface - 1.5f));
    }

    protected override string Tag => "GRIP";

    protected override string Failed => "did not happen in";

    protected override void Observe(float delta)
    {
        _upsSeen.Add(Player.SurfaceUp);
        if (Player.CurrentState == Player.States.Grip)
        {
            _highestGrip = Mathf.Max(_highestGrip, Player.Center.Y);
            _lowestGrip = Mathf.Min(_lowestGrip, Player.Center.Y);
        }

        _maxAbsZ = Mathf.Max(_maxAbsZ, Mathf.Abs(Player.GlobalPosition.Z));
    }

    protected override void Ending()
    {
        if (_maxAbsZ > 0.001f)
        {
            Problems.Add($"left the play plane (|Z| up to {_maxAbsZ})");
        }
    }

    protected override string Detail => $"at {Player.GlobalPosition}, state {Player.CurrentState.Name}, up {Player.SurfaceUp}";

    protected override string Outcome =>
        $"{StepCount} steps (ledge assist, wall, inner corner, outer corners, ceiling, leap, let go, metal's end, water)";
}
