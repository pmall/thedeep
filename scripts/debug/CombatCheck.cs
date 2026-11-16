using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// Scripted combat and session check in the mechanics lab (`-- --room MechanicsLab --combat-check`, run by
/// tools/check.sh), through the real input actions: lingering in front of the Votary gets the pilgrim struck;
/// swings break the Votary; a deadly blow brings the pilgrim back whole at the room's spawn, the room renewed;
/// resting at the shrine heals and makes it the place to come back to; walking into the relic takes its
/// ability. Prints `COMBAT OK` or `COMBAT CHECK` errors.
/// </summary>
public partial class CombatCheck : StepDriver
{
    /// <summary>The running game's session.</summary>
    public required Session Session { get; init; }

    private bool _dealt;

    /// <summary>A blow too heavy to survive, once the grace period of an earlier hit is over.</summary>
    private void DeadlyBlow()
    {
        if (!_dealt && Player.Vitality.Vulnerable)
        {
            _dealt = Player.TakeHit(100, new System.Numerics.Vector2(18f, 0f));
        }
    }

    private Machine? Votary => World.Room.First<Machine>();

    protected override void Build()
    {
        Steps.Add(new("get struck lingering in front of the Votary", 5f,
            () => Player.Respawn(new Vector3(27.5f, 0f, 0f), 1),
            () => Player.Vitality.Current < Player.Vitality.Max));
        Steps.Add(new("break the Votary with swings", 6f,
            () => Player.Respawn(new Vector3(27.6f, 0f, 0f), 1),
            () => Votary is { Broken: true },
            () =>
            {
                // Keep in reach (a hit pushes it back), and press every 21 ticks (a swing and its cooldown is about 20).
                Hold(InputActions.MoveRight, Votary is { } v && v.PlanePosition.X - Player.GlobalPosition.X > 1.5f);
                if (Ticks % 21 == 1)
                {
                    Press(InputActions.Attack);
                }
                else if (Ticks % 21 == 3)
                {
                    Release(InputActions.Attack);
                }
            }));
        Steps.Add(new("die and come back whole at the spawn point, the Votary renewed", 6f,
            () =>
            {
                Release(InputActions.Attack);
                Release(InputActions.MoveRight);
                Player.Respawn(new Vector3(20f, 0f, 0f), 1);
                _dealt = false;
            },
            () => _dealt && Player.Vitality.Current == Player.Vitality.Max && Player.InControl
                  && Player.GlobalPosition.DistanceTo(World.Room.SpawnPoint.GlobalPosition) < 0.6f && Votary is { Broken: false },
            DeadlyBlow));
        Steps.Add(new("rest at the shrine", 2f,
            () =>
            {
                Player.Respawn(new Vector3(4.5f, 0f, 0f), 1);
                Player.TakeHit(1, new System.Numerics.Vector2(10f, 0f));
            },
            () => Session.RestedShrine == "Shrine" && Player.Vitality.Current == Player.Vitality.Max && Ticks > 57,
            () =>
            {
                // Pressed once the pilgrim has landed from the blow's knockback, back by the shrine.
                if (Ticks == 50)
                {
                    Player.Respawn(new Vector3(4.5f, 0f, 0f), 1);
                }
                else if (Ticks == 55)
                {
                    Press(InputActions.Interact);
                }
                else if (Ticks == 57)
                {
                    Release(InputActions.Interact);
                }
            }));
        Steps.Add(new("die and come back at the shrine", 6f,
            () =>
            {
                Player.Respawn(new Vector3(20f, 0f, 0f), 1);
                _dealt = false;
            },
            () => _dealt && Player.InControl && Player.Vitality.Current == Player.Vitality.Max
                  && Player.GlobalPosition.DistanceTo(new Vector3(4f, 0f, 0f)) < 0.6f,
            DeadlyBlow));
        Steps.Add(new("walk into the relic and take its ability", 4f,
            () => { Player.Respawn(new Vector3(13f, 0f, 0f), 1); Press(InputActions.MoveRight); },
            () => Player.Abilities.Has(Ability.Drill),
            () =>
            {
                if (Ticks == 30)
                {
                    Release(InputActions.MoveRight);
                }
            }));
    }

    protected override string Tag => "COMBAT";

    protected override string Failed => "did not happen in";

    protected override string Detail =>
        $"at {Player.GlobalPosition}, health {Player.Vitality.Current}/{Player.Vitality.Max}, state {Player.CurrentState.Name}";

    protected override string Outcome => $"{StepCount} steps (struck, swings break a machine, death and return, rest, relic)";
}
