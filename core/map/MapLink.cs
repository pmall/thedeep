using System.Numerics;

namespace TheDeep;

/// <summary>A connection as the layout sees it: the exit's marker in its room and the arrival marker in the target room.</summary>
public sealed record MapLink(string From, string FromExit, Vector2 FromPosition, string To, string ToExit, Vector2 ToPosition);
