using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace TheDeep;

/// <summary>A visited room as the map screen draws it: its name, its tile (screen pixels, y down), and whether the pilgrim is in it.</summary>
public sealed record MapTile(string Name, Box Rect, bool Here);
