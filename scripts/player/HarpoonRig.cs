using System.Collections.Generic;
using Godot;
using TheDeep.States;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// The Harpoon's eye and line (in Main). Each physics tick it picks the anchor the pilgrim would fire at
/// (<see cref="HarpoonAim"/>: in range, in clear sight, aimed by held up/down input, else ahead and up) and
/// hands it to <see cref="Player.AnchorInSight"/>, and tells every anchor of the room what to show (<see cref="AnchorLook"/>).
/// Each frame it draws the line from the pilgrim to the flying head, taut while it pulls, reeling back in when it lets go.
/// </summary>
public partial class HarpoonRig : Node3D
{
    /// <summary>How fast the line reels back in after letting go (m/s).</summary>
    private const float ReelSpeed = 90f;

    [Export] public World World { get; set; } = null!;
    [Export] public Player Player { get; set; } = null!;

    /// <summary>A unit-height cylinder along Y, stretched between the pilgrim and the head.</summary>
    [Export] public MeshInstance3D Line { get; set; } = null!;

    /// <summary>The harpoon's head, at the line's end.</summary>
    [Export] public Node3D Head { get; set; } = null!;

    private readonly List<HarpoonAnchor> _anchors = new();
    private readonly List<NVector2> _points = new();
    private PlaneRay? _ray;
    private PlaneRay Ray => _ray ??= PlaneRay.For(PhysicsLayers.Solid);
    private readonly System.Func<int, bool> _inSight;
    private NVector2 _sightFrom;

    /// <summary>The anchor the pilgrim would fire at if it had the Harpoon (null with it): a ring out of reach, for the gate hints.</summary>
    public HarpoonAnchor? OutOfReach { get; private set; }
    private float _reel;
    private Vector3 _reelEnd;

    public HarpoonRig()
    {
        _inSight = i => InSight(_sightFrom, _points[i]); // made once: a lambda per tick would allocate
    }

    public override void _Ready()
    {
        TopLevel = true;
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        Ray.Attach(this);
        World.RoomLoaded += OnRoomLoaded;
        OnRoomLoaded(World.Room);
        Line.Visible = false;
        Head.Visible = false;
    }

    public override void _ExitTree()
    {
        World.RoomLoaded -= OnRoomLoaded;
    }

    public override void _PhysicsProcess(double delta)
    {
        HarpoonAnchor? target = null;
        OutOfReach = !Player.Abilities.Has(Ability.Harpoon) && _anchors.Count > 0 && !Player.Frozen ? Pick() : null;
        if (Player.CurrentState is HarpoonState harpoon)
        {
            target = harpoon.Anchor;
        }
        else if (Player.Abilities.Has(Ability.Harpoon) && _anchors.Count > 0 && !Player.Frozen)
        {
            target = Pick();
        }

        Player.AnchorInSight = Player.CurrentState is HarpoonState ? null : target;
        bool armed = Player.Abilities.Has(Ability.Harpoon);
        foreach (HarpoonAnchor anchor in _anchors)
        {
            if (IsInstanceValid(anchor))
            {
                anchor.Look = LookOf(anchor, target, armed);
            }
        }
    }

    public override void _Process(double delta)
    {
        Vector3 hand = Player.GetGlobalTransformInterpolated().Origin + new Vector3(0f, Player.CenterHeight + 0.15f, 0f);
        if (Player.CurrentState is HarpoonState { Anchor: { } fired } harpoon && IsInstanceValid(fired))
        {
            Vector3 anchor = fired.GlobalPosition;
            Vector3 toAnchor = anchor - hand;
            float reach = Mathf.Min(harpoon.Line + 0.15f, toAnchor.Length());
            _reelEnd = hand + toAnchor.Normalized() * reach;
            _reel = 1f;
            Draw(hand, harpoon.Bitten ? anchor : _reelEnd);
            return;
        }

        if (_reel > 0f)
        {
            // Reeling in: the head flies back to the hand.
            Vector3 back = hand - _reelEnd;
            float step = ReelSpeed * (float)delta;
            _reelEnd = back.Length() <= step ? hand : _reelEnd + back.Normalized() * step;
            _reel = _reelEnd.IsEqualApprox(hand) ? 0f : 1f;
            if (_reel > 0f)
            {
                Draw(hand, _reelEnd);
                return;
            }
        }

        Line.Visible = false;
        Head.Visible = false;
    }

    private void Draw(Vector3 from, Vector3 to)
    {
        Vector3 span = to - from;
        float length = span.Length();
        if (length < 0.05f)
        {
            Line.Visible = false;
            Head.Visible = false;
            return;
        }

        Vector3 y = span / length;
        var x = new Vector3(y.Y, -y.X, 0f);
        Line.GlobalTransform = new Transform3D(new Basis(x, y * length, Vector3.Back), from + span * 0.5f);
        Head.GlobalTransform = new Transform3D(new Basis(x, y, Vector3.Back), to);
        Line.Visible = true;
        Head.Visible = true;
    }

    private HarpoonAnchor? Pick()
    {
        PlayerTuning t = Player.Tuning;
        NVector2 center = Player.Center;
        PlayerInput input = Player.CurrentInput;
        var aim = input.MoveY != 0 ? new NVector2(input.MoveX, input.MoveY) : NVector2.Zero;
        int facing = input.MoveX != 0 ? input.MoveX : Player.WorldFacing;
        _points.Clear();
        foreach (HarpoonAnchor anchor in _anchors)
        {
            _points.Add(anchor.Point);
        }

        _sightFrom = center;
        int skip = Player.LastAnchor is { } last ? _anchors.IndexOf(last) : -1;
        int index = HarpoonAim.Pick(center, aim, facing, _points, t.HarpoonRange, t.HarpoonMinDistance, _inSight, skip);
        return index >= 0 ? _anchors[index] : null;
    }

    private AnchorLook LookOf(HarpoonAnchor anchor, HarpoonAnchor? target, bool armed)
    {
        if (!armed)
        {
            return AnchorLook.Dormant;
        }

        if (Player.CurrentState is HarpoonState { Anchor: { } fired } harpoon && fired == anchor)
        {
            return harpoon.Bitten ? AnchorLook.Bitten : AnchorLook.Sighted;
        }

        if (anchor == target)
        {
            return AnchorLook.Sighted;
        }

        if (anchor == Player.LastAnchor)
        {
            return AnchorLook.Spent;
        }

        PlayerTuning t = Player.Tuning;
        float distance = (anchor.Point - Player.Center).Length();
        return HarpoonAim.InReach(distance, t.HarpoonRange, t.HarpoonMinDistance) ? AnchorLook.Ready : AnchorLook.Far;
    }

    private bool InSight(NVector2 from, NVector2 to)
    {
        NVector2 end = to - NVector2.Normalize(to - from) * HarpoonAim.SightMargin;
        return !Ray.Cast(from.OnPlane(), end.OnPlane());
    }

    private void OnRoomLoaded(Room room)
    {
        _anchors.Clear();
        _anchors.AddRange(room.All<HarpoonAnchor>());
        OutOfReach = null;
        _reel = 0f;
        Player.AnchorInSight = null;
        Player.LastAnchor = null;
    }
}
