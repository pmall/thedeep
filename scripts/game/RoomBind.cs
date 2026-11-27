using System.Collections.Generic;
using System.Linq;

namespace TheDeep;

/// <summary>
/// What a room's bricks need from the game when it loads, done once per room: they are shown as the pilgrimage left them
/// (<see cref="Settle"/>) and handed the pilgrim and the banner they act on (<see cref="Give"/>). What the session reacts to
/// (a lift released, a block shattered, a law broken) it subscribes to itself.
/// </summary>
public static class RoomBind
{
    /// <summary>What the pilgrimage holds done in <paramref name="room"/> (<paramref name="name"/>) is shown done: relics empty, rock and laws gone.</summary>
    public static void Settle(Room room, string name, Pilgrimage pilgrimage, Ability unlocked)
    {
        IReadOnlyList<AbilityRelic> relics = room.All<AbilityRelic>();
        for (int i = 0; i < relics.Count; i++)
        {
            if (pilgrimage.RelicEmpty(name, relics[i].NameText(), relics[i].Ability, unlocked))
            {
                relics[i].Empty();
            }
        }

        foreach (Block block in room.Blocks().Where(b => b.Cracked))
        {
            if (pilgrimage.Done(name, block.NameText()))
            {
                block.Settle();
            }
        }

        foreach (LawBreak law in room.All<LawBreak>())
        {
            if (pilgrimage.Done(name, law.NameText()))
            {
                law.Settle();
            }
        }
    }

    /// <summary>Gives the room's heat, slag, lifts, laws and scripture plates the pilgrim, banner and prompt arbiter they act on.</summary>
    public static void Give(Room room, Player player, Banner banner, PromptArbiter prompts)
    {
        foreach (HeatColumn zone in room.All<HeatColumn>())
        {
            zone.Player = player;
        }

        foreach (SlagBody slag in room.All<SlagBody>())
        {
            slag.Player = player;
        }

        foreach (Lift lift in room.All<Lift>())
        {
            lift.Player = player;
        }

        foreach (ScripturePlate plate in room.All<ScripturePlate>())
        {
            plate.Banner = banner;
        }

        foreach (LawBreak law in room.All<LawBreak>())
        {
            law.Player = player;
            law.Prompts = prompts;
        }
    }
}
