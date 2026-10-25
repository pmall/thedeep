using Godot;

namespace TheDeep;

/// <summary>
/// A pilgrim shrine: a niche with glowing faith glyphs where the pilgrim rests. Resting (interact) saves
/// the game and restores health; dying brings the pilgrim back to the last shrine rested at. The node is
/// where the pilgrim stands to rest, on the floor in front of the niche.
/// </summary>
public partial class Shrine : Node3D
{
    /// <summary>The glyphs' light: takes the biome's glyph color (the room lists it in its GlyphLights).</summary>
    [Export] public OmniLight3D Light { get; set; } = null!;
}
