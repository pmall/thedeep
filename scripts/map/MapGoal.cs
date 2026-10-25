using Godot;

namespace TheDeep;

/// <summary>
/// Where the journey ends, for the map solver (docs/long-term.md, objective 2): from every state the pilgrim
/// can reach, a room holding a goal must stay reachable. The M6 slice ends at the Descent; the full game will
/// end at the core.
/// </summary>
public partial class MapGoal : Marker3D
{
}
