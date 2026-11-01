using Godot;

namespace TheDeep;

/// <summary>
/// One depth layer of a world's descent (docs/vision.md#setting): the name its network file gives its rooms (<c>room Narthex Flooded</c>)
/// and the biome its rooms wear.
/// </summary>
[GlobalClass]
public partial class WorldLayer : Resource
{
    [Export] public string Name { get; set; } = "";

    [Export] public BiomeProfile Biome { get; set; } = null!;
}
