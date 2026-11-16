using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TheDeep;

/// <summary>
/// What a room is for in the plan (docs/roadmap.md#stage-plan): the closed set a room table's <c>kinds</c> column draws from. Some
/// kinds are facts the network states too, and <see cref="RoomTable.Check"/> holds them to it: <see cref="Start"/>, <see cref="Goal"/>,
/// <see cref="Relic"/>, <see cref="Boss"/>, <see cref="Ambush"/>, <see cref="PowerUp"/>, <see cref="Milestone"/> (iff the network says so) and
/// <see cref="Secret"/> (every way in hidden). The others are design intent the network can't see.
/// </summary>
public enum RoomKind
{
    /// <summary>Where a new game wakes (the network's <c>start</c>).</summary>
    Start,

    /// <summary>Where the journey ends (a <c>goal</c>).</summary>
    Goal,

    /// <summary>A crossroads several ways meet at.</summary>
    Hub,

    /// <summary>A way between places, little else.</summary>
    Corridor,

    /// <summary>An ability lies here (a <c>relic</c>).</summary>
    Relic,

    /// <summary>A boss is fought here (a <c>boss</c>).</summary>
    Boss,

    /// <summary>A story beat tied to depth happens here (a <c>milestone</c>: a law break).</summary>
    Milestone,

    /// <summary>A shrine to rest and save at.</summary>
    Shrine,

    /// <summary>A room off the map's open ways: every way in is <c>hidden</c>.</summary>
    Secret,

    /// <summary>An enemy trap: the room seals while a wave stands (an <c>ambush</c>).</summary>
    Ambush,

    /// <summary>A power-up lies here (a <c>powerup</c>: strength or defense, for good).</summary>
    PowerUp,

    /// <summary>A puzzle: a switch and its mechanism, a code, a door to work out.</summary>
    Enigma,

    /// <summary>Scripture, a conversation, something to learn.</summary>
    Lore,
}
