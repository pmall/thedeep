using Godot;

namespace TheDeep;

/// <summary>What the scripted checks share besides their runner (<see cref="StepDriver"/>): pressing the real input actions, and
/// looking at where things stand.</summary>
public static class CheckKit
{
    private static readonly StringName[] Played =
    {
        InputActions.Interact, InputActions.Attack, InputActions.MoveRight, InputActions.MoveLeft, InputActions.MoveUp,
        InputActions.MoveDown, InputActions.Dash, InputActions.Jump, InputActions.Harpoon,
    };

    /// <summary>Presses <paramref name="action"/> if <paramref name="held"/>, releases it if not.</summary>
    public static void Hold(StringName action, bool held)
    {
        if (held)
        {
            Input.ActionPress(action);
        }
        else
        {
            Input.ActionRelease(action);
        }
    }

    /// <summary>One tick of a fight through the real inputs: closes on <paramref name="foe"/> (a hovering one only while it hangs low),
    /// faces it, jumps for it, and swings every 21 ticks (a swing and its cooldown is about 20) once close; <paramref name="ticks"/>
    /// counts from the fight's start. The caller keeps the pilgrim alive if the rules, not the balance, are what it checks.</summary>
    public static void Brawl(Player player, Machine foe, int ticks)
    {
        Vector2 at = foe.PlanePosition;
        float gap = at.X - player.GlobalPosition.X, rise = at.Y - player.GlobalPosition.Y;
        bool hovers = foe.Species.Locomotion == Locomotion.Hover;
        bool reachable = !hovers || foe.Intent.Act is MachineAct.Recover or MachineAct.Strike;
        float near = hovers ? 1.2f : 1.4f;
        Hold(InputActions.MoveRight, reachable && gap > near);
        Hold(InputActions.MoveLeft, reachable && gap < -near);
        if (Mathf.Abs(gap) <= near && Mathf.Sign(gap) != player.WorldFacing && gap != 0f)
        {
            Hold(gap > 0f ? InputActions.MoveRight : InputActions.MoveLeft, true);
        }

        Hold(InputActions.Jump, hovers && reachable && rise > 2.2f && Mathf.Abs(gap) < 2.5f && player.IsOnFloor() && ticks % 30 < 14);
        Hold(InputActions.Attack, Mathf.Abs(gap) < 2.6f && ticks % 21 is 1 or 2);
    }

    /// <summary>Lets go of every action the pilgrim plays with.</summary>
    public static void ReleaseAll()
    {
        foreach (StringName action in Played)
        {
            Input.ActionRelease(action);
        }
    }

    /// <summary>Where the pilgrim stands to be talked to: on the floor under the conversation, a little short of it.</summary>
    public static Vector3 Porch(Conversation conversation)
    {
        Vector3 from = conversation.GlobalPosition + new Vector3(-2f, 0.5f, 0f);
        var ray = PhysicsRayQueryParameters3D.Create(from, from + Vector3.Down * 30f, PhysicsLayers.Solid);
        using Godot.Collections.Dictionary hit = conversation.GetWorld3D().DirectSpaceState.IntersectRay(ray);
        if (hit.Count == 0)
        {
            return new Vector3(from.X, 0f, 0f);
        }

        using Variant key = "position";
        using Variant position = hit[key];
        return position.AsVector3() with { Z = 0f };
    }

    /// <summary>Whether <paramref name="prompt"/> stands on screen, within a few meters of <paramref name="player"/>.</summary>
    public static bool InSight(Node3D prompt, Player player)
    {
        Camera3D camera = prompt.GetViewport().GetCamera3D();
        Rect2 screen = prompt.GetViewport().GetVisibleRect();
        Vector3 at = prompt.GlobalPosition;
        return !camera.IsPositionBehind(at) && screen.HasPoint(camera.UnprojectPosition(at))
               && new Vector2(at.X - player.GlobalPosition.X, at.Y - player.GlobalPosition.Y).Length() < 3.5f;
    }
}
