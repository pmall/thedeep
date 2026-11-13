using System;
using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// A trap room's arena (a room kind, docs/vision.md#game-systems; an <see cref="Arena"/> whose foes are a wave of machines,
/// <see cref="Wave"/>): the wave is not there until the pilgrim steps inside, then it stands up at its places as the gates fall.
/// The world's network names it by its room (<c>ambush &lt;Room&gt;</c>).
/// </summary>
public partial class Ambush : Arena
{
    /// <summary>The machines that spring the trap, placed where they stand up.</summary>
    [Export] public Machine[] Wave { get; set; } = Array.Empty<Machine>();

    /// <summary>How hard the camera shakes as the last of the wave breaks (<see cref="SideCamera.AddShake"/>).</summary>
    [Export] public float ClearedShake { get; set; } = 0.35f;

    /// <inheritdoc/>
    public override IEnumerable<Machine> Foes => Wave;

    public override void _Ready()
    {
        foreach (Machine machine in Wave)
        {
            machine.Gone(); // not there until the trap springs
        }
    }

    /// <inheritdoc/>
    protected override bool FoesStand(bool waiting) => waiting ? Wave.Length > 0 : Array.Exists(Wave, m => !m.Broken);

    /// <summary>The wave stands up at its places.</summary>
    protected override void OnBegan()
    {
        foreach (Machine machine in Wave)
        {
            machine.Reset();
        }
    }
}
