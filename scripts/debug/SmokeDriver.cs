using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// Scripted smoke test (run by tools/check.sh with `-- --smoke`): presses run, jump and dash
/// through the real input actions, then checks the player actually moved and visited every
/// base movement state. Prints "SMOKE OK" and quits, or reports an error and quits with code 1.
/// </summary>
public partial class SmokeDriver : Node
{
    private const int EndTick = 330;
    private const float MinFullJumpHeight = 2.9f;
    private const float MinRunDistance = 10f;

    private readonly HashSet<string> _statesSeen = new();
    private int _tick;
    private Vector3 _start;
    private float _maxX;
    private float _maxHeightInFullJump;
    private float _fullJumpBaseY;
    private float _maxAbsZ;

    public Player Player { get; set; } = null!;

    public SmokeDriver()
    {
        // Run before the player each tick so "just pressed" lands on the same physics frame.
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    public override void _PhysicsProcess(double delta)
    {
        _tick++;
        switch (_tick)
        {
            case 20:
                _start = Player.GlobalPosition;
                Input.ActionPress(InputActions.MoveRight);
                break;
            case 50:
                Input.ActionPress(InputActions.Jump); // short hop
                break;
            case 53:
                Input.ActionRelease(InputActions.Jump);
                break;
            case 100:
                Input.ActionPress(InputActions.Dash);
                break;
            case 102:
                Input.ActionRelease(InputActions.Dash);
                break;
            case 140: // clear of PlatformB, so the full jump below has headroom
                Input.ActionRelease(InputActions.MoveRight);
                break;
            case 170:
                _fullJumpBaseY = Player.GlobalPosition.Y;
                Input.ActionPress(InputActions.Jump); // full jump, held
                break;
            case 230:
                Input.ActionRelease(InputActions.Jump);
                break;
            case 260:
                Input.ActionPress(InputActions.MoveLeft);
                Input.ActionPress(InputActions.Dash); // air or ground dash back left
                break;
            case 262:
                Input.ActionRelease(InputActions.Dash);
                break;
            case 300:
                Input.ActionRelease(InputActions.MoveLeft);
                break;
        }

        _statesSeen.Add(Player.CurrentState.Name);
        _maxX = Mathf.Max(_maxX, Player.GlobalPosition.X);
        _maxAbsZ = Mathf.Max(_maxAbsZ, Mathf.Abs(Player.GlobalPosition.Z));
        if (_tick is > 170 and < 260)
        {
            _maxHeightInFullJump = Mathf.Max(_maxHeightInFullJump, Player.GlobalPosition.Y - _fullJumpBaseY);
        }

        if (_tick == EndTick)
        {
            Finish();
        }
    }

    private void Finish()
    {
        var problems = new List<string>();
        float travelled = _maxX - _start.X;
        if (travelled < MinRunDistance)
        {
            problems.Add($"player only moved {travelled:0.00} m right");
        }

        if (_maxHeightInFullJump < MinFullJumpHeight)
        {
            problems.Add($"full jump only reached {_maxHeightInFullJump:0.00} m");
        }

        if (_maxAbsZ > 0.001f)
        {
            problems.Add($"player left the play plane (|z| = {_maxAbsZ})");
        }

        foreach (string state in new[] { "Idle", "Run", "Jump", "Fall", "Dash" })
        {
            if (!_statesSeen.Contains(state))
            {
                problems.Add($"state {state} never entered");
            }
        }

        if (problems.Count == 0)
        {
            GD.Print($"SMOKE OK: moved {travelled:0.00} m, full jump {_maxHeightInFullJump:0.00} m, states {string.Join(",", _statesSeen)}");
            GetTree().Quit();
        }
        else
        {
            GD.PushError("SMOKE FAIL: " + string.Join("; ", problems));
            GetTree().Quit(1);
        }
    }
}
