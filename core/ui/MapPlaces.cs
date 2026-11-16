
namespace TheDeep;

/// <summary>
/// Where the map screen draws, screen pixels on the game's canvas: the largest scale (<see cref="MaxScale"/>, pixels per meter), the
/// margin the rooms keep from the canvas's sides and top, the legend's baseline above the canvas's bottom (the last of the items held
/// on it too, the others a row each over it), the room kept between the rooms and the words under them, the gap between two rooms'
/// tiles, and the legend's mark (its width before the words) and the spacing between its entries.
/// </summary>
public readonly record struct MapPlaces(float MaxScale, float Margin, float Baseline, float Row, float Clear, float Gap, float Mark, float Spacing)
{
    /// <summary>The places the map screen lays out with: its exports start from these, and the layout's tests read them.</summary>
    public static readonly MapPlaces Default = new(MaxScale: 6f, Margin: 96f, Baseline: 80f, Row: 24f, Clear: 48f, Gap: 8f, Mark: 18f, Spacing: 30f);
}
