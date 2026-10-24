using Godot;

namespace TheDeep;

/// <summary>
/// Scripted play for test drivers: runs right and left in turns, hopping and dashing now and then,
/// so the camera moves and the frames change. Call <see cref="Tick"/> once per physics tick.
/// </summary>
public sealed class DemoInput
{
    private int _tick;

    public void Tick()
    {
        _tick++;
        int phase = _tick % 240;
        if (phase == 0)
        {
            Input.ActionRelease(InputActions.MoveLeft);
            Input.ActionPress(InputActions.MoveRight);
        }
        else if (phase == 120)
        {
            Input.ActionRelease(InputActions.MoveRight);
            Input.ActionPress(InputActions.MoveLeft);
        }

        Pulse(InputActions.Jump, period: 50, held: 20);
        Pulse(InputActions.Dash, period: 97, held: 2);
    }

    /// <summary>Lets go of every action this script presses.</summary>
    public static void ReleaseAll()
    {
        Input.ActionRelease(InputActions.MoveLeft);
        Input.ActionRelease(InputActions.MoveRight);
        Input.ActionRelease(InputActions.Jump);
        Input.ActionRelease(InputActions.Dash);
    }

    private void Pulse(StringName action, int period, int held)
    {
        if (_tick % period == 0)
        {
            Input.ActionPress(action);
        }
        else if (_tick % period == held)
        {
            Input.ActionRelease(action);
        }
    }
}
