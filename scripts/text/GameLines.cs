using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// The line ids the code names itself, whatever the world: prompts, the map's legend, the end card, and those its kinds name
/// (<see cref="GateKinds"/>, <see cref="ItemKinds"/>, <see cref="PowerStats"/>, the <see cref="ControlsManual"/>). The room check
/// holds every world's book to <see cref="Named"/> (<see cref="LineCheck"/>). The end card's lines are each world's to give; the
/// others are in the game's own book.
/// </summary>
public static class GameLines
{
    public static readonly StringName Talk = "ui.talk";
    public static readonly StringName Pull = "ui.pull";
    public static readonly StringName Rest = "ui.rest";
    public static readonly StringName Read = "ui.read";
    public static readonly StringName MapWayOpen = "map.way.open";
    public static readonly StringName MapWayUnbuilt = "map.way.unbuilt";
    public static readonly StringName EndingTitle = "ending.title";
    public static readonly StringName EndingLine = "ending.line";
    public static readonly StringName EndingThanks = "ending.thanks";

    /// <summary>Every line id the code names.</summary>
    public static IEnumerable<StringName> Named()
    {
        foreach (StringName id in new[] { Talk, Pull, Rest, Read, MapWayOpen, MapWayUnbuilt, EndingTitle, EndingLine, EndingThanks })
        {
            yield return id;
        }

        foreach (Ability ability in System.Enum.GetValues<Ability>())
        {
            if (ability != Ability.None)
            {
                yield return GateKinds.HintLine(ability);
                yield return GateKinds.MapWord(ability);
            }
        }

        foreach (ItemKind kind in System.Enum.GetValues<ItemKind>())
        {
            yield return ItemKinds.MapWord(kind);
        }

        foreach (PowerStat stat in System.Enum.GetValues<PowerStat>())
        {
            yield return PowerStats.NameLine(stat);
            yield return PowerStats.HintLine(stat);
        }

        foreach (ControlsManual.Spec spec in ControlsManual.Gameplay)
        {
            yield return spec.Line;
        }

        foreach (ControlsManual.Spec spec in ControlsManual.Debug)
        {
            yield return spec.Line;
        }
    }
}
