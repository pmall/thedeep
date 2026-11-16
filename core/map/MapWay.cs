using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// A way out of a visited room as the map knows it: the exit's name, where it sits in its room and which way it leads out of it, what
/// it needs (a <see cref="Requirement"/> expression), whether the room behind it is built, and whether it is hidden (a secret's way in:
/// the map shows it only once taken).
/// </summary>
public sealed record MapWay(string Room, string Exit, Vector2 Position, ExitSide Side, string Requires, bool Built, bool Hidden = false);
