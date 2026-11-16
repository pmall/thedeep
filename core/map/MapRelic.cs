using System.Collections.Generic;
using System.Linq;

namespace TheDeep;

/// <summary>An ability relic lying in a room (taken on entering it).</summary>
public sealed record MapRelic(string Room, string Name, Ability Ability);
