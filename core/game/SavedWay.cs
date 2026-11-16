
namespace TheDeep;

/// <summary>
/// A way out of a visited room on the saved map (<see cref="MapWay"/>). Its side is missing from a save written before it was kept
/// (<see cref="SaveData.MapWays"/>).
/// </summary>
public sealed record SavedWay(string Room, string Exit, float X, float Y, string Requires, bool Built, bool Hidden = false, ExitSide? Side = null);
