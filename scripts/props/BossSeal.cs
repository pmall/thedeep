using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// A boss's arena (an <see cref="Arena"/> whose foe is one machine, <see cref="Boss"/>): its name is shown when the fight begins and its
/// fall when it breaks; once beaten, the boss is gone for good. The world's network names it by its boss's species
/// (<c>boss &lt;Room&gt; &lt;DisplayName&gt;</c>).
/// </summary>
public partial class BossSeal : Arena, IAnnounces
{
    [Export] public Machine Boss { get; set; } = null!;

    /// <summary>Banner lines when the fight starts (the boss's name) and when it ends.</summary>
    [Export] public StringName NameLine { get; set; } = "";
    [Export] public StringName FallenLine { get; set; } = "";
    [Export] public StringName FallenHintLine { get; set; } = "";

    /// <summary>How hard the camera shakes as the boss breaks (<see cref="SideCamera.AddShake"/>).</summary>
    [Export] public float FallenShake { get; set; } = 0.5f;

    /// <inheritdoc/>
    public IEnumerable<StringName> LineIds => new[] { NameLine, FallenLine, FallenHintLine };

    /// <summary>What the banner says as the fight begins: the boss's name alone.</summary>
    public Announcement Named => new(NameLine, "");

    /// <summary>What the banner says as the boss breaks.</summary>
    public Announcement Fallen => new(FallenLine, FallenHintLine);

    /// <inheritdoc/>
    public IEnumerable<Announcement> Announcements => new[] { Named, Fallen };

    /// <inheritdoc/>
    public override IEnumerable<Machine> Foes => new[] { Boss };

    /// <summary>Beaten before: no boss, the gates up.</summary>
    public override void Settle()
    {
        base.Settle();
        Boss.Gone();
    }

    /// <inheritdoc/>
    protected override bool FoesStand(bool waiting) => !Boss.Broken;
}
