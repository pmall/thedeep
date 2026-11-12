using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// Renders the game's screens for a look (`-- --ui-shot &lt;dir&gt;`, windowed: headless renders nothing): a gate hint over the
/// room with the gameplay controls for the keyboard, Jump held, and the debug keys (hint.png), the controls for the gamepad, Dash
/// held (manual-pad.png), the dialogue box on a whole line (dialogue.png), the pilgrim at the start room's first shrine, plate or
/// lever with its prompt offered, keyboard then gamepad (prompt.png, prompt-pad.png); where the room holds a law, the pilgrim first
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

    public override void _Ready()
    {
        DirAccess.MakeDirRecursiveAbsolute(OutputDir);
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
