using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// Renders the game's screens for a look (`-- --ui-shot &lt;dir&gt;`, windowed: headless renders nothing): a gate hint over the
/// room with the gameplay controls for the keyboard, Jump held, and the debug keys (hint.png), a blow that leaves one hit (hurt.png,
/// the health a moment later: last-hit.png), the controls for the gamepad, Dash
/// held (manual-pad.png), the dialogue box on a whole line (dialogue.png), the pilgrim at the start room's first shrine, plate or
/// lever with its prompt offered, keyboard then gamepad (prompt.png, prompt-pad.png), an ability's banner once eased in
/// (banner.png), and where the start room holds a <see cref="Proclamation"/>, the pilgrim back there stood in it, its words
/// spoken (proclamation.png); where the room holds a law, the pilgrim first
/// presses on at its lip until it refuses, then holds the act offered there to two-thirds (prompt-law.png: the refusal on the hint
/// line, the hold's ring part-filled), then, after crossing a few open exits from the start room,
/// the map screen (map.png), then the end card
/// (end.png). For judging UI as
/// images, like the look-dev captures; not part of tools/check.sh.
/// </summary>
public partial class UiShot : Node
{
    /// <summary>The running game's world: its rooms, and the one loaded.</summary>
    public required World World { get; init; }
    /// <summary>The running game's pilgrim.</summary>
    public required Player Player { get; init; }
    /// <summary>The running game's travel between rooms.</summary>
    public required RoomTravel Travel { get; init; }
    /// <summary>The running game's hint line.</summary>
    public required HintLine Hint { get; init; }
    /// <summary>The running game's map screen.</summary>
    public required MapScreen Map { get; init; }
    /// <summary>The running game's end card.</summary>
    public required EndCard EndCard { get; init; }
    /// <summary>The running game's banner.</summary>
    public required Banner Banner { get; init; }
    /// <summary>The controls manual to shoot, if the game shows one.</summary>
    public ControlsManual? Manual { get; set; }
    /// <summary>The manual of the debug keys, shown for the hint shot only; null for none.</summary>
    public ControlsManual? DebugKeys { get; set; }
    /// <summary>The dialogue box to shoot; null skips that shot's box.</summary>
    public DialogueBox? Dialogue { get; set; }
    /// <summary>The folder the images are written to.</summary>
    public string OutputDir { get; set; } = "";

    /// <summary>How many exits to cross before the map is shown.</summary>
    public int Hops { get; set; } = 4;

    private int _tick;
    private int _hops;
    private bool _mapped;
    private bool _shotEnd;
    private float _endTime;
    private readonly HashSet<string> _visited = new();
    private LawBreak? _law;
    private double? _banner;
    private string _bannerShot = "banner.png";
    private string _start = "";

    public override void _Ready()
    {
        DirAccess.MakeDirRecursiveAbsolute(OutputDir);
        _start = World.RoomName;
        ProcessMode = ProcessModeEnum.Always;
        Hint.Show(GateKinds.HintLine(Ability.MagGrip), GateKinds.ColorOf(Ability.MagGrip));
    }

    public override void _Process(double delta)
    {
        if (_law is { } law)
        {
            HoldLaw(law);
            return;
        }

        if (_banner is { } since)
        {
            // Shot once eased in, timed in seconds: the banner's tween runs on time, not frames.
            _banner = since + delta;
            if (_banner >= (_bannerShot == "banner.png" ? 0.9 : 1.5))
            {
                Shot(_bannerShot);
                _banner = null;
                if (_bannerShot == "banner.png" && Proclaimed() is Proclamation spoken)
                {
                    // Stood in it, so the speaker is in view; the session says it too, which the banner reads as the same words.
                    Banner.Clear();
                    Player.GlobalPosition = new Vector3(spoken.GlobalPosition.X, spoken.GlobalPosition.Y, 0f);
                    Player.ResetPhysicsInterpolation();
                    Banner.Show(spoken.Said);
                    _bannerShot = "proclamation.png";
                    _banner = 0.0;
                }
            }

            return;
        }

        _tick++;
        if (_tick == 70)
        {
            if (DebugKeys is not null)
            {
                DebugKeys.Visible = true;
            }

            SetDevice(ButtonNames.Device.Keyboard);
            Input.ActionPress(InputActions.Jump);
        }
        else if (_tick == 90)
        {
            Shot("hint.png");
            if (DebugKeys is not null)
            {
                DebugKeys.Visible = false;
            }

            Hint.Clear();
            Input.ActionRelease(InputActions.Jump);
            SetDevice(ButtonNames.Device.Gamepad);
        }
        else if (_tick == 94)
        {
            Player.TakeHit(Player.Vitality.Current - 1, Player.Feet + new System.Numerics.Vector2(-1f, 0f));
        }
        else if (_tick == 98)
        {
            Shot("hurt.png");
        }
        else if (_tick == 114)
        {
            Shot("last-hit.png");
        }
        else if (_tick == 118)
        {
            Player.Vitality.Restore();
        }
        else if (_tick == 120)
        {
            Input.ActionPress(InputActions.Dash);
        }
        else if (_tick == 130)
        {
            Shot("manual-pad.png");
            Input.ActionRelease(InputActions.Dash);
            SetDevice(ButtonNames.Device.Keyboard);
            if (LineBook.Shared.Find("dialogue.lamplighter.light") is { } line)
            {
                Dialogue?.Show(line.Speaker, line.Text, line.Text.Length, whole: true);
            }
        }
        else if (_tick == 150)
        {
            Shot("dialogue.png");
            Dialogue?.Close();
            if (!StandAtLaw())
            {
                StandAtProp();
            }
        }
        else if (_tick == 200)
        {
            Shot("prompt.png");
            // A stick pushed past its wake (the right one: it moves nothing) puts the prompts on the pad.
            Input.ParseInputEvent(new InputEventJoypadMotion { Axis = JoyAxis.RightX, AxisValue = 0.9f });
            Input.ParseInputEvent(new InputEventJoypadMotion { Axis = JoyAxis.RightX, AxisValue = 0f });
        }
        else if (_tick == 215)
        {
            Shot("prompt-pad.png");
            Banner.Show(new Announcement("ability.mag_grip.name", "ability.mag_grip.hint"));
            _banner = 0.0;
        }
        else if (_tick > 230 && _tick % 20 == 0 && _hops < Hops)
        {
            _visited.Add(World.RoomName);
            RoomExit? exit = World.Room.Exits().Where(e => e.Open && ResourceLoader.Exists(e.TargetRoom))
                .OrderBy(e => _visited.Contains(World.NameOf(e.TargetRoom)) ? 1 : 0).FirstOrDefault();
            if (exit is not null)
            {
                Travel.Go(exit);
            }

            _hops++;
        }
        else if (_hops == Hops && !_mapped)
        {
            _mapped = true;
            Map.Toggle();
            _tick = 1000;
        }
        else if (_tick == 1060)
        {
            Shot("map.png");
            Map.Toggle();
            _ = EndCard.Play();
        }
        else if (_tick > 1060 && (_endTime += (float)delta) >= 7.5f && !_shotEnd)
        {
            _shotEnd = true;
            Shot("end.png");
            GetTree().Paused = false;
            GetTree().Quit();
        }
    }

    /// <summary>Stands the pilgrim where the room's first prop that prompts is taken (a shrine, a plate, a lever), if it has one.</summary>
    private void StandAtProp()
    {
        if ((First<Shrine>() ?? First<ScripturePlate>() ?? First<Lever>()) is { } prop)
        {
            Player.GlobalPosition = new Vector3(prop.GlobalPosition.X + 0.5f, prop.GlobalPosition.Y + 0.05f, 0f);
            Player.ResetPhysicsInterpolation();
        }
    }

    /// <summary>Stands the pilgrim before the room's first law, if it has one, pressing on toward it; false for none.</summary>
    private bool StandAtLaw()
    {
        if (First<LawBreak>() is not LawBreak { Broken: false } law)
        {
            return false;
        }

        _law = law;
        Player.GlobalPosition = new Vector3(law.GlobalPosition.X - law.Toward * 0.8f, law.GlobalPosition.Y + 0.05f, 0f);
        Player.ResetPhysicsInterpolation();
        Input.ActionPress(law.Toward > 0 ? InputActions.MoveRight : InputActions.MoveLeft);
        return true;
    }

    /// <summary>Presses on until the pilgrim refuses, then holds the act offered; shot two-thirds full, then let go.</summary>
    private void HoldLaw(LawBreak law)
    {
        if (law.Prompt.Offered)
        {
            Input.ActionRelease(law.Toward > 0 ? InputActions.MoveRight : InputActions.MoveLeft);
            Input.ActionPress(InputActions.Interact);
        }

        if (law.Prompt.Fill >= 0.66f)
        {
            Shot("prompt-law.png");
            Input.ActionRelease(InputActions.Interact);
            _law = null;
        }
    }

    /// <summary>The start room's first proclamation, the room loaded again if the pilgrim has left it (a dash out of it); null for none.</summary>
    private Proclamation? Proclaimed()
    {
        if (World.RoomName != _start)
        {
            World.Load(GD.Load<PackedScene>(World.RoomPath(_start)));
        }

        return First<Proclamation>() as Proclamation;
    }

    private Node3D? First<T>() where T : Node3D => World.Room.All<T>() is { Count: > 0 } all ? all[0] : null;

    private void SetDevice(ButtonNames.Device device)
    {
        if (Manual is not null)
        {
            Manual.Device = device;
        }
    }

    private void Shot(string name)
    {
        string path = $"{OutputDir}/{name}";
        Error e = GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print(e == Error.Ok ? $"[ui-shot] {path}" : $"[ui-shot] could not save {path}: {e}");
    }
}
