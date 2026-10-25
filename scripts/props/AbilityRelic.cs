using Godot;

namespace TheDeep;

/// <summary>
/// A relic holding one of the pilgrim's abilities (Mag-grip, later Harpoon, Drill, Ballast, Heat plating),
/// resting on an altar. Taking it unlocks the ability. The node stands on the floor where the pilgrim
/// takes it. Pickup, persistence and its moment come with the save system (M6).
/// </summary>
public partial class AbilityRelic : Node3D
{
    [Export] public Ability Ability { get; set; } = Ability.MagGrip;
}
