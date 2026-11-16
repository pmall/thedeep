using System;
using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// A scripture plate read in place (`-- --room Sanctum --plate-check`, run by tools/check.sh), through the real input actions: stood
/// at the room's first <see cref="ScripturePlate"/>, "Read" is offered on screen by the pilgrim; interact has the banner show its
/// title and words as the book writes them, and its glyphs glow; they glow as long as the words are on screen, and settle once they
/// have gone. Each step has a time limit. Prints `PLATE OK` or `PLATE CHECK` errors.
/// </summary>
public partial class PlateCheck : StepDriver
{
    /// <summary>The running game's banner.</summary>
    public required Banner Banner { get; init; }

    private bool _dimmed;

    protected override void Build()
    {
        if (World.Room.First<ScripturePlate>() is not { } plate)
        {
            Fail($"the {World.RoomName} has no scripture plate");
            return;
        }

        string title = LineBook.Shared.Find(plate.TitleLine)?.Text ?? "";
        Steps.Add(new("stood at the plate, \"Read\" is offered on screen by the pilgrim", 3f,
            () => Player.Respawn(plate.GlobalPosition + new Vector3(0.3f, 0.05f, 0f), 1),
            () => plate.Prompt.Offered && CheckKit.InSight(plate.Prompt, Player) && plate.Glowing == 0f));
        Steps.Add(new("interact shows its title and words in the banner, and its glyphs glow", 1.5f,
            () => Press(InputActions.Interact),
            () => Banner.Current == plate.Said && Banner.ShownTitle == title && title.Length > 0 && plate.Glowing >= 0.99f));
        Steps.Add(new("they glow as long as the words are on screen", 20f,
            () => Release(InputActions.Interact),
            () =>
            {
                _dimmed |= Banner.Current == plate.Said && plate.Glowing < 0.99f;
                return !Banner.Showing;
            }));
        Steps.Add(new("and settle once the words have gone", plate.GlowFade + 0.5f,
            () => { },
            () => plate.Glowing == 0f && !_dimmed));
    }

    protected override string Tag => "PLATE";

    protected override string Detail => $"at {Player.GlobalPosition}, banner {Banner.Current}, title \"{Banner.ShownTitle}\", dimmed early {_dimmed}";

    protected override string Outcome => $"{StepCount} steps (offered on screen, read into the banner, glowing while shown, settled after)";
}
