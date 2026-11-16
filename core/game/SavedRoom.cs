
namespace TheDeep;

/// <summary>A visited room on the saved map.</summary>
public sealed record SavedRoom(string Name, float X, float Y, float MinX, float MinY, float MaxX, float MaxY);
