using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// Scripted check of the flooded layer's end (`-- --room Chapterhouse --encounter-check`, run by tools/check.sh), through the
/// real input actions. In the Chapterhouse: stepping into the arena drops both gates; the Precentor strikes the pilgrim
/// lingering before it; swings break it; the gates rise and the fight is remembered; entering again, it stays broken and
/// the gates stay up. At the Brink: the third law holds the pilgrim at the lip and it says why; pressing on three times the old
/// break time, or tapping the act's button, never breaks it; a hold let go at half starts again from zero, and the full hold
/// breaks it (remembered); over
/// the lip, the jump falls on into Rimefall and lands there. In the Choir: walking past a sleeping Chorister wakes none;
/// a hard landing beside one wakes it. In the Counterweight: standing in the cage lets go of its brake, and it carries the
/// pilgrim (standing on its floor all the way) up into the Undercroft, where the shaft's head cage lifts it to the floor. In the Calving: Mag-grip climbs the
/// girder up the ice cliff and the cliff's top leads back into the Coldstacks. In the Bore: the shaft drops into the
/// chamber, the relic gives the Drill, a dash bores the cracked wall (remembered), the tunnel's steps climb back to the
/// hall. In the Sounding: the drop into the well, floating to the shelf, the relic gives Ballast; sunk under the well's
/// wall, rising in the shaft beyond, out onto the east hall. In the Mere: sunk through a lead to the lake bed and down its
/// shaft into the Underice; the tanks blown, back up into the Mere. In the Coldstacks: a dive through the ice plug drops
/// into the Vault's pool; out of it and into the arena, both gates fall; the Archivist dives at the pilgrim below it; swings
/// break it while it hangs low; the gates rise, remembered. In the Sanctum: the Hierarch's order is heard coming near, spoken
/// (the banner names its speaker over the words, no title); the second law holds the pilgrim at the forbidden door, and the hint
/// line says why; holding the act breaks it (remembered), and the refusal leaves the hint line at once. Prints `ENCOUNTER OK` or
/// `ENCOUNTER CHECK` errors.
/// </summary>
public partial class EncounterCheck : StepDriver
{
    private static readonly StringName PrecentorId = "precentor";
    private static readonly StringName ChoristerId = "chorister";
    private static readonly StringName ArchivistId = "archivist";

    /// <summary>The running game's session.</summary>
    public required Session Session { get; init; }
    /// <summary>The running game's banner.</summary>
    public required Banner Banner { get; init; }
    /// <summary>The running game's hint line.</summary>
    public required HintLine Hint { get; init; }

    private BossSeal Seal => World.Room.First<BossSeal>() ?? throw new InvalidOperationException("no BossSeal in the room");
    private Machine Precentor => World.Room.All<Machine>().First(m => m.Species.Id == PrecentorId);
    private LawBreak Law => World.Room.First<LawBreak>() ?? throw new InvalidOperationException("no LawBreak in the room");
    private IEnumerable<Machine> Choristers => World.Room.All<Machine>().Where(m => m.Species.Id == ChoristerId);
    private Lift Cage => World.Room.First<Lift>() ?? throw new InvalidOperationException("no Lift in the room");
    private Machine Archivist => World.Room.All<Machine>().First(m => m.Species.Id == ArchivistId);
    private bool _ordered;
    private float X => Player.GlobalPosition.X;
    private float _rodeTo;
    private bool _rideProblem;
    private int _jumpHeld;
    private bool _rose;
    private int _brokeAt;

    protected override void Build()
    {
        Steps.Add(new("walk into the arena: both gates fall", 4f,
            () => { Player.Respawn(new Vector3(3f, 0f, 0f), 1); Hold(InputActions.MoveRight, true); },
            () => Seal.Fighting && Seal.Gates.All(g => g.Shut),
            () => Hold(InputActions.MoveRight, X < 12f)));
        Steps.Add(new("the Precentor strikes the pilgrim lingering before it", 8f,
            () => { Hold(InputActions.MoveRight, false); Player.Respawn(new Vector3(26.5f, 0f, 0f), 1); },
            () => Player.Vitality.Current < Player.Vitality.Max));
        Steps.Add(new("swings break the Precentor", 40f,
            () => { },
            () => Precentor.Broken,
            () =>
            {
                // Kept alive (the check is about the fight's rules, not its balance), close to it and facing it.
                Player.Vitality.Restore();
                float gap = Precentor.PlanePosition.X - X;
                Hold(InputActions.MoveRight, gap > 1.4f);
                Hold(InputActions.MoveLeft, gap < -1.4f);
                if (Mathf.Abs(gap) <= 1.4f && Math.Sign(gap) != Player.WorldFacing && gap != 0f)
                {
                    Hold(gap > 0f ? InputActions.MoveRight : InputActions.MoveLeft, true);
                }

                Hold(InputActions.Attack, Ticks % 21 is 1 or 2);
            }));
        Steps.Add(new("the gates rise and the fight is remembered", 4f,
            () => { Hold(InputActions.MoveRight, false); Hold(InputActions.MoveLeft, false); Hold(InputActions.Attack, false); },
            () => Seal.Gates.All(g => g.Raised) && Session.Done("Chapterhouse", "Fight")));
        Steps.Add(new("entering again: the Precentor stays broken, the gates stay up", 3f,
            () =>
            {
                World.Load(GD.Load<PackedScene>(World.RoomPath("Chapterhouse")));
                Player.Respawn(new Vector3(3f, 0f, 0f), 1);
                Hold(InputActions.MoveRight, true);
            },
            () => Ticks > 120 && X > 12f && Precentor.Broken && Seal.Gates.All(g => g.Raised) && !Seal.Fighting,
            () => Hold(InputActions.MoveRight, X < 14f)));
        Steps.Add(new("at the Brink, the third law holds the pilgrim at the lip, and it says why", 3f,
            () =>
            {
                Hold(InputActions.MoveRight, false);
                World.Load(GD.Load<PackedScene>(World.RoomPath("Brink")));
                Player.Respawn(new Vector3(18f, 0f, 0f), 1);
                Hold(InputActions.MoveRight, true);
            },
            () => Ticks >= 75 && X < 24f && Player.IsOnFloor() && !Law.Broken && Law.Strain > 0.1f && Law.Refused));
        Steps.Add(new("pressing on three times the old break time never breaks it; the act is offered", 9f,
            () => { },
            () => Ticks >= 3 * 156 && !Law.Broken && Law.Strain < 1f && Law.Prompt.Offered));
        Steps.Add(new("tapping the act's button every 0.2 s for 5 s never breaks it", 6f,
            () => Hold(InputActions.MoveRight, false),
            () => Ticks >= 300 && !Law.Broken && Law.Prompt.Offered,
            () => Hold(InputActions.Interact, Ticks % 12 < 6)));
        Steps.Add(new("a hold let go at half starts from zero; the full hold breaks it, remembered", 5f,
            () => { Hold(InputActions.Interact, false); _brokeAt = -1; },
            () => _brokeAt >= 0 && Session.Done("Brink", "ThirdLaw"),
            () =>
            {
                int hold = Mathf.RoundToInt(Law.Prompt.HoldSeconds * 60f);
                Hold(InputActions.Interact, (Ticks <= hold / 2 || Ticks > hold / 2 + 5) && !Law.Broken);
                if (Ticks == hold / 2 && Law.Prompt.Fill < 0.45f)
                {
                    Problems.Add($"held to half, the hold filled only {Law.Prompt.Fill:0.00}");
                }

                if (Ticks == hold / 2 + 3 && Law.Prompt.Fill > 0f)
                {
                    Problems.Add($"let go at half, the hold kept {Law.Prompt.Fill:0.00}");
                }

                if (_brokeAt < 0 && Law.Broken)
                {
                    _brokeAt = Ticks;
                    if (Ticks - (hold / 2 + 5) < hold - 2)
                    {
                        Problems.Add($"held again after half, the law broke after {Ticks - (hold / 2 + 5)} ticks, short of the full {hold}");
                    }
                }
            }));
        Steps.Add(new("over the lip, the jump falls on into Rimefall and lands on its shelf", 12f,
            () => { },
            () => World.RoomName == "Rimefall" && Player.InControl && Player.IsOnFloor() && Player.GlobalPosition.Y > 9f,
            () => Hold(InputActions.MoveRight, World.RoomName == "Brink" && Player.GlobalPosition.Y > -2f)));
        Steps.Add(new("in the Choir, walking past a sleeping Chorister wakes none", 4f,
            () =>
            {
                Hold(InputActions.MoveRight, false);
                World.Load(GD.Load<PackedScene>(World.RoomPath("Choir")));
                Player.Respawn(new Vector3(13f, -2f, 0f), 1);
                Hold(InputActions.MoveRight, true);
            },
            () => X > 20f && Choristers.All(c => c.Brain.Act == MachineAct.Sleep),
            () => Hold(InputActions.MoveRight, X < 20.5f)));
        Steps.Add(new("a hard landing beside one wakes it", 3f,
            () => { Hold(InputActions.MoveRight, false); Player.Respawn(new Vector3(24f, 3f, 0f), 1); },
            () => Choristers.Single(c => c.NameText() == "Chorister1").Brain.Act != MachineAct.Sleep
                  && Choristers.Single(c => c.NameText() == "Chorister0").Brain.Act == MachineAct.Sleep));
        Steps.Add(new("in the Counterweight, standing in the cage lets go of its brake", 4f,
            () =>
            {
                World.Load(GD.Load<PackedScene>(World.RoomPath("Counterweight")));
                Player.Respawn(new Vector3(8f, 0f, 0f), 1);
                Hold(InputActions.MoveRight, true);
            },
            () => Cage.Travel.Phase == LiftPhase.Rising,
            () => Hold(InputActions.MoveRight, X < 12.6f)));
        Steps.Add(new("it carries the pilgrim up the shaft into the Undercroft, its head cage onto the floor", 16f,
            () => { _rodeTo = 0f; },
            () => World.RoomName == "Undercroft" && Player.InControl && Player.IsOnFloor() && Mathf.Abs(X - 42f) < 2.1f
                  && Mathf.Abs(Player.GlobalPosition.Y) < 0.05f && _rodeTo > 23f,
            () =>
            {
                if (World.RoomName != "Counterweight")
                {
                    return;
                }

                // Riding, not left behind: the pilgrim's feet stay on the cage floor all the way up.
                float above = Player.GlobalPosition.Y - Cage.Cage.GlobalPosition.Y;
                _rodeTo = Mathf.Max(_rodeTo, Player.GlobalPosition.Y);
                if (Mathf.Abs(above) > 0.25f && !_rideProblem)
                {
                    _rideProblem = true;
                    Problems.Add($"the pilgrim left the cage floor on the way up ({above:0.00} m from it, the cage {Cage.Travel.Offset:0.0} m up)");
                }
            }));
        Steps.Add(new("in the Calving, the girder climbs the ice cliff back to the Coldstacks' doorway (Mag-grip)", 10f,
            () =>
            {
                World.Load(GD.Load<PackedScene>(World.RoomPath("Calving")));
                Player.Abilities.Unlock(Ability.MagGrip);
                Player.Respawn(new Vector3(25f, 0f, 0f), 1);
                Hold(InputActions.MoveRight, true);
            },
            () => World.RoomName == "Coldstacks",
            () =>
            {
                // Into the girder, up it, then east along the cliff's top to the doorway.
                bool climbing = Player.CurrentState == Player.States.Grip;
                Hold(InputActions.MoveUp, climbing);
                Hold(InputActions.MoveRight, !climbing || Player.GlobalPosition.Y > 15.5f);
            }));
        Steps.Add(new("in the Bore, the shaft drops into the chamber and the relic gives the Drill", 10f,
            () =>
            {
                Hold(InputActions.MoveUp, false);
                Hold(InputActions.MoveRight, false);
                Player.Abilities.Lock(Ability.MagGrip);
                World.Load(GD.Load<PackedScene>(World.RoomPath("Bore")));
                Player.Respawn(new Vector3(22f, 0f, 0f), -1);
                Hold(InputActions.MoveLeft, true);
            },
            () => Player.Abilities.Has(Ability.Drill) && Player.InControl,
            () => Hold(InputActions.MoveLeft, Player.InControl && X > 4.5f)));
        Steps.Add(new("a dash bores through the cracked wall, remembered; the tunnel's steps climb back to the hall", 10f,
            () => { Hold(InputActions.MoveLeft, false); Hold(InputActions.MoveRight, true); },
            () => X > 43f && X < 46f && Player.IsOnFloor() && Mathf.Abs(Player.GlobalPosition.Y) < 0.05f
                  && Session.Done("Bore", "CrackedWall"),
            () =>
            {
                Hold(InputActions.Dash, X > 16.5f && X < 19f && Ticks % 20 < 2);
                // Full jumps up each step: pressed on the floor, held a while.
                if (X > 26f && X < 41f && Player.IsOnFloor() && _jumpHeld <= 0)
                {
                    _jumpHeld = 26;
                }

                Hold(InputActions.Jump, --_jumpHeld > 0);
            }));
        Steps.Add(new("in the Sounding, the drop into the well, floating to the shelf: the relic gives Ballast", 10f,
            () =>
            {
                Hold(InputActions.Jump, false);
                World.Load(GD.Load<PackedScene>(World.RoomPath("Sounding")));
                Player.Respawn(new Vector3(10f, 0f, 0f), 1);
            },
            () => Player.Abilities.Has(Ability.Ballast) && Player.InControl,
            () =>
            {
                // Over the lip, then floating: a full jump onto the shelf at the waterline.
                bool floating = Player.CurrentState == Player.States.Swim && Player.Velocity.Y > -0.5f;
                Hold(InputActions.MoveRight, X < 20.5f);
                if (floating && _jumpHeld <= -10)
                {
                    _jumpHeld = 26;
                }

                Hold(InputActions.Jump, --_jumpHeld > 0);
            }));
        Steps.Add(new("sunk under the well's wall and up the shaft beyond, out onto the east hall", 12f,
            () => { Hold(InputActions.Jump, false); Hold(InputActions.MoveRight, false); _jumpHeld = 0; },
            () => X > 34.5f && Player.IsOnFloor() && Player.GlobalPosition.Y > -4.1f && Player.CurrentState != Player.States.Swim,
            () =>
            {
                // Off the shelf into the water, down to the floor, east under the wall; in the shaft, blow the tanks, rise, jump out.
                bool beyond = X > 30.5f, onShelf = Player.IsOnFloor() && Player.CurrentState != Player.States.Swim && X < 30f;
                Hold(InputActions.MoveLeft, onShelf);
                Hold(InputActions.MoveDown, !beyond && !onShelf);
                Hold(InputActions.MoveUp, beyond && Player.Sunk);
                Hold(InputActions.MoveRight, !onShelf);
                if (beyond && !Player.Sunk && Player.CurrentState == Player.States.Swim && Player.Velocity.Y > -0.5f && _jumpHeld <= -10)
                {
                    _jumpHeld = 26;
                }

                Hold(InputActions.Jump, --_jumpHeld > 0);
            }));
        Steps.Add(new("in the Mere, sunk through a lead to the lake bed and down its shaft into the Underice", 12f,
            () =>
            {
                Hold(InputActions.Jump, false);
                Hold(InputActions.MoveUp, false);
                Hold(InputActions.MoveLeft, false);
                Hold(InputActions.MoveRight, false);
                World.Load(GD.Load<PackedScene>(World.RoomPath("Mere")));
                Player.Respawn(new Vector3(21.5f, 1f, 0f), 1);
            },
            () => World.RoomName == "Underice",
            () =>
            {
                Hold(InputActions.MoveDown, true);
                Hold(InputActions.MoveRight, X < 31.5f);
            }));
        Steps.Add(new("in the Underice, sunk clear of the shaft, then the tanks blown: up the shaft, back into the Mere", 10f,
            () => Hold(InputActions.MoveRight, false),
            () => World.RoomName == "Mere" && Player.InControl,
            () =>
            {
                bool clear = World.RoomName == "Underice" && Player.InControl && Player.GlobalPosition.Y < 19f;
                _rose |= clear;
                Hold(InputActions.MoveDown, !_rose);
                Hold(InputActions.MoveUp, _rose);
            }));
        Steps.Add(new("in the Coldstacks, down and dash on the ice plug: the dive breaks it and drops into the Vault's pool", 8f,
            () =>
            {
                Hold(InputActions.MoveUp, false);
                Player.Abilities.Unlock(Ability.Drill);
                World.Load(GD.Load<PackedScene>(World.RoomPath("Coldstacks")));
                Player.Respawn(new Vector3(64f, -10f, 0f), 1);
                Hold(InputActions.MoveDown, true);
            },
            () => World.RoomName == "Vault" && Player.InControl && Player.CurrentState == Player.States.Swim,
            () => Hold(InputActions.Dash, World.RoomName == "Coldstacks" && Ticks is > 10 and < 14)));
        Steps.Add(new("out of the pool and west into the arena: both gates fall", 8f,
            () => { Hold(InputActions.MoveDown, false); Hold(InputActions.Dash, false); Hold(InputActions.MoveLeft, true); _jumpHeld = 0; },
            () => Seal.Fighting && Seal.Gates.All(g => g.Shut),
            () =>
            {
                if (Player.CurrentState == Player.States.Swim && Player.Velocity.Y > -0.5f && _jumpHeld <= -10)
                {
                    _jumpHeld = 26;
                }

                Hold(InputActions.Jump, --_jumpHeld > 0);
                Hold(InputActions.MoveLeft, X > 30f);
            }));
        Steps.Add(new("the Archivist dives at the pilgrim lingering below it", 12f,
            () => { Hold(InputActions.Jump, false); Hold(InputActions.MoveLeft, false); },
            () => Player.Vitality.Current < Player.Vitality.Max));
        Steps.Add(new("swings break the Archivist while it hangs after its dives", 90f,
            () => { },
            () => Archivist.Broken,
            () =>
            {
                // Kept alive (the rules, not the balance); under it while it hangs low, swinging, jumping to reach it.
                Player.Vitality.Restore();
                Vector2 at = Archivist.PlanePosition;
                float gap = at.X - X, rise = at.Y - Player.GlobalPosition.Y;
                bool low = Archivist.Intent.Act is MachineAct.Recover or MachineAct.Strike;
                Hold(InputActions.MoveRight, low && gap > 1.2f);
                Hold(InputActions.MoveLeft, low && gap < -1.2f);
                if (Mathf.Abs(gap) <= 1.2f && Math.Sign(gap) != Player.WorldFacing && gap != 0f)
                {
                    Hold(gap > 0f ? InputActions.MoveRight : InputActions.MoveLeft, true);
                }

                Hold(InputActions.Jump, low && rise > 2.2f && Mathf.Abs(gap) < 2.5f && Player.IsOnFloor() && Ticks % 30 < 14);
                Hold(InputActions.Attack, Mathf.Abs(gap) < 2.6f && Ticks % 21 is 1 or 2);
            }));
        Steps.Add(new("the Vault's gates rise and the fight is remembered", 4f,
            () =>
            {
                foreach (StringName action in new[] { InputActions.MoveRight, InputActions.MoveLeft, InputActions.Attack, InputActions.Jump })
                {
                    Hold(action, false);
                }
            },
            () => Seal.Gates.All(g => g.Raised) && Session.Done("Vault", "Arena")));
        Steps.Add(new("in the Sanctum, coming near the Hierarch: its order is heard", 6f,
            () =>
            {
                Session.Spoke += OnSpoke;
                World.Load(GD.Load<PackedScene>(World.RoomPath("Sanctum")));
                Player.Respawn(new Vector3(40f, 0f, 0f), -1);
                Hold(InputActions.MoveLeft, true);
            },
            () => _ordered));
        Steps.Add(new("the second law holds the pilgrim at the forbidden door, and it says why", 4f,
            () => { },
            () => Ticks >= 90 && X > 7f && Player.IsOnFloor() && !Law.Broken && Law.Strain > 0.1f && Law.Refused
                  && Hint.Current == Law.RefusalLine));
        Steps.Add(new("holding the act breaks it, remembered, the refusal withdrawn at once; past it, the end of what is built", 6f,
            () => _brokeAt = -1,
            () => Law.Broken && Session.Done("Sanctum", "Law") && X < 6f,
            () =>
            {
                Hold(InputActions.Interact, Law.Prompt.Offered && !Law.Broken);
                if (_brokeAt < 0 && Law.Broken)
                {
                    _brokeAt = Ticks;
                    if (Hint.Current == Law.RefusalLine)
                    {
                        Problems.Add($"the law broken, the hint line still says the refusal {Law.RefusalLine}");
                    }
                }
            }));
    }

    /// <summary>The Hierarch's order heard: the banner speaks it, its speaker named over the words, no title.</summary>
    private void OnSpoke(Proclamation proclamation)
    {
        if (proclamation.NameText() != "Order")
        {
            return;
        }

        _ordered = true;
        string speaker = LineBook.Shared.Find(proclamation.SpeakerLine)?.Text ?? "";
        if (Banner.Current != proclamation.Said || proclamation.Said.Voice != Voice.Spoken || Banner.ShownSpeaker != speaker || speaker == ""
            || Banner.ShownTitle != "")
        {
            Problems.Add($"the order was not spoken: the banner shows {Banner.Current?.ToString() ?? "nothing"}, speaker \"{Banner.ShownSpeaker}\", " +
                          $"title \"{Banner.ShownTitle}\" (the speaker {proclamation.SpeakerLine} is \"{speaker}\")");
        }
    }

    protected override string Tag => "ENCOUNTER";

    protected override string Failed => "did not happen in";

    protected override string Detail =>
        $"in {World.RoomName} at {Player.GlobalPosition}, health {Player.Vitality.Current}/{Player.Vitality.Max}, state {Player.CurrentState.Name}";

    protected override void Ending() => Session.Spoke -= OnSpoke;

    protected override string Outcome =>
        $"{StepCount} steps (arena gates, the Precentor struck and broken, remembered; the third law held, refused, never pushed or tapped through, broken by the full hold, the fall into Rimefall; the Choir asleep, woken; the Counterweight's ride up; the Calving's girder; the Bore's relic, its cracked wall, its steps; the Sounding's relic, under its wall; the Mere's lake bed down into the Underice and back; the ice plug into the Vault, its gates, the Archivist broken, remembered; the Hierarch's order spoken, the second law held and refused, broken by the hold, the refusal withdrawn)";
}
