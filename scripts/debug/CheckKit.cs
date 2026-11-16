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
