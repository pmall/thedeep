using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// Scripted Harpoon check in the mechanics lab (`-- --room MechanicsLab --harpoon-check`, run by tools/check.sh),
/// through the real input actions: no anchor is in sight without the ability; with it, the near anchor is; firing
/// pulls the pilgrim to it and lets go rising, the air dash back; firing again chains to the next anchor (the one
/// just left is skipped); an anchor behind a solid or out of range is never in sight; a jump during the pull
/// cancels into a jump; the edge pointer shows the anchor in sight only when it is off screen. Each step has a time limit. Prints `HARPOON OK` or `HARPOON CHECK` errors.
/// </summary>
public partial class HarpoonCheck : StepDriver
{
    /// <summary>The running game's pointer to a Harpoon target off screen.</summary>
    public required AnchorPointer Pointer { get; init; }

    private float _maxAbsZ;
    private float _closest = float.MaxValue;

    private HarpoonAnchor A => World.Room.All<HarpoonAnchor>().First(a => a.NameText() == "AnchorA");

    private HarpoonAnchor B => World.Room.All<HarpoonAnchor>().First(a => a.NameText() == "AnchorB");

    protected override void Build()
    {
        float margin = Pointer.Margin;
        Steps.Add(new("no anchor in sight without the Harpoon", 0.3f,
            () => { Player.Abilities.Lock(Ability.Harpoon); Place(11f, 0f); },
            () => Player.AnchorInSight is not null, Hold: true));
        Steps.Add(new("with it, the near anchor is in sight", 0.3f,
            () => Player.Abilities.Unlock(Ability.Harpoon),
            () => Player.AnchorInSight == A));
        Steps.Add(new("the anchor framed: no pointer at the screen's edge", 0.3f,
            () => { },
            () => Pointer.Pointing, Hold: true));
        Steps.Add(new("the anchor unframed (an edge wider than the screen): the pointer shows", 0.3f,
            () => Pointer.Margin = 100000f,
            () => Pointer.Pointing && Player.AnchorInSight == A));
        Steps.Add(new("fire: the line flies out and the pull begins", 0.3f,
            () => { Pointer.Margin = margin; Press(InputActions.Harpoon); },
            () => Player.CurrentState == Player.States.Harpoon && Player.States.Harpoon.Bitten));
        Steps.Add(new("reach the anchor, let go rising with the air dash back", 1.5f,
            () => { Release(InputActions.Harpoon); _closest = float.MaxValue; },
            () => Player.LastAnchor == A && Player.CurrentState == Player.States.Fall && Player.Velocity.Y > 0f
                  && Player.AirDashesLeft == Player.Tuning.AirDashes && _closest < 0.3f));
        Steps.Add(new("the next anchor is in sight (the one just left is skipped)", 0.2f,
            () => { },
            () => Player.AnchorInSight == B));
        Steps.Add(new("fire again: chain to the next anchor", 1.5f,
            () => Press(InputActions.Harpoon),
            () => Player.LastAnchor == B));
        Steps.Add(new("an anchor behind a solid is not in sight", 0.3f,
            () => { Release(InputActions.Harpoon); Place(14f, 13f); },
            () => Player.AnchorInSight is not null, Hold: true));
        Steps.Add(new("an anchor out of range is not in sight", 0.3f,
            () => Place(2f, 0f),
            () => Player.AnchorInSight is not null, Hold: true));
        Steps.Add(new("fire, and jump during the pull: a jump", 1f,
            () => { Place(11f, 0f); Press(InputActions.Harpoon); },
            () => Player.CurrentState == Player.States.Harpoon && Player.States.Harpoon.Bitten));
        Steps.Add(new("…cancels the pull into a jump", 0.3f,
            () => { Release(InputActions.Harpoon); Press(InputActions.Jump); },
            () => Player.CurrentState == Player.States.Jump));
    }

    protected override string Tag => "HARPOON";

    protected override void Observe(float delta)
    {
        _maxAbsZ = Mathf.Max(_maxAbsZ, Mathf.Abs(Player.GlobalPosition.Z));
        if (Player.CurrentState == Player.States.Harpoon)
        {
            _closest = Player.States.Harpoon.Anchor is { } anchor ? Mathf.Min(_closest, (anchor.Point - Player.Center).Length()) : _closest;
        }
    }

    protected override void Ending()
    {
        if (_maxAbsZ > 0.001f)
        {
            Problems.Add($"left the play plane (|Z| up to {_maxAbsZ})");
        }
    }

    protected override string Detail =>
        $"at {Player.GlobalPosition}, state {Player.CurrentState.Name}, in sight {Player.AnchorInSight?.NameText() ?? "none"}, " +
        $"last {Player.LastAnchor?.NameText() ?? "none"}, closest {_closest:0.00} m";

    protected override string Outcome => $"{StepCount} steps (sight, fire, pull, release, chain, blocked, range, jump cancel)";
}
