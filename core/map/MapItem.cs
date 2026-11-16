using System.Collections.Generic;
using System.Linq;

namespace TheDeep;

/// <summary>An item lying in a room (a key, a code; taken on entering it, held for good), by its id.</summary>
public sealed record MapItem(string Room, string Name, string Item);
