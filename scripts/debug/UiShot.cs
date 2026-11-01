using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// Renders the game's screens for a look (`-- --ui-shot &lt;dir&gt;`, windowed: headless renders nothing): a gate hint over the
/// room with the gameplay controls for the keyboard, Jump held, and the debug keys (hint.png), the controls for the gamepad, Dash
/// held (manual-pad.png), then, after crossing a few open exits from the start room, the map screen (map.png), then the end card
/// (end.png). For judging UI as
/// images, like the look-dev captures; not part of tools/check.sh.
/// </summary>
public partial class UiShot : Node
{
    public World World { get; set; } = null!;
    public Player Player { get; set; } = null!;
    public RoomTravel Travel { get; set; } = null!;
    public HintLine Hint { get; set; } = null!;
    public MapScreen Map { get; set; } = null!;
    public EndCard EndCard { get; set; } = null!;
    public ControlsManual? Manual { get; set; }
    public ControlsManual? DebugKeys { get; set; }
    public string OutputDir { get; set; } = "";

    /// <summary>How many exits to cross before the map is shown.</summary>
    public int Hops { get; set; } = 4;

    private int _tick;
    private int _hops;
    private bool _mapped;
    private bool _shotEnd;
    private float _endTime;
    private readonly HashSet<string> _visited = new();

    public override void _Ready()
    {
        DirAccess.MakeDirRecursiveAbsolute(OutputDir);
        ProcessMode = ProcessModeEnum.Always;
        Hint.Show(GateKinds.HintLine(Ability.MagGrip), GateKinds.ColorOf(Ability.MagGrip));
    }

    public override void _Process(double delta)
    {
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
        }
        else if (_tick > 130 && _tick % 20 == 0 && _hops < Hops)
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
