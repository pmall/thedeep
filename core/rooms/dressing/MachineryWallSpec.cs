namespace TheDeep;

/// <summary>The numbers of a machinery wall (<see cref="MachineryWall"/>): sizes in meters, shares of the wall, a bay or a gear, chances 0 to 1, depths out of the wall.</summary>
public sealed record MachineryWallSpec
{
    /// <summary>The approved look.</summary>
    public static readonly MachineryWallSpec Default = new();

    /// <summary>A gear's teeth stand this far apart along its pitch circle, so meshed gears share their teeth's size.</summary>
    public float ToothPitch { get; init; } = 0.5f;

    /// <summary>Two gears mesh with their middles this share of their radii's sum apart (their pitch circles touching).</summary>
    public float GearMesh { get; init; } = 0.92f;

    /// <summary>The fewest teeth a gear has.</summary>
    public int MinTeeth { get; init; } = 10;

    /// <summary>How deep a bank stands out from its back.</summary>
    public float BankDepth { get; init; } = 0.7f;

    /// <summary>A bank's wind chest is its height times this...</summary>
    public float ChestShare { get; init; } = 0.16f;

    /// <summary>...at least this high...</summary>
    public float ChestMin { get; init; } = 0.35f;

    /// <summary>...and at most this.</summary>
    public float ChestMax { get; init; } = 0.7f;

    /// <summary>A piston's width.</summary>
    public float PistonWidth { get; init; } = 0.45f;

    /// <summary>How far a piston stands out of the wall.</summary>
    public float PistonDepth { get; init; } = 0.4f;

    /// <summary>The chance a piston is cut short (the machine stopped mid-stroke).</summary>
    public float ShortPistonChance { get; init; } = 0.3f;

    /// <summary>A short piston's least height as a share of the wall's.</summary>
    public float ShortPistonShare { get; init; } = 0.45f;

    /// <summary>What a short piston's height may add to <see cref="ShortPistonShare"/>, as a share of the wall's.</summary>
    public float ShortPistonSpread { get; init; } = 0.3f;

    /// <summary>The band's height is the wall's times this, at most <see cref="BandMax"/>.</summary>
    public float BandShare { get; init; } = 0.3f;

    /// <summary>The band's greatest height up the wall.</summary>
    public float BandMax { get; init; } = 5f;

    /// <summary>A wall taller than this also gets a high band.</summary>
    public float TallWall { get; init; } = 16f;

    /// <summary>The high band's height as a share of the wall's.</summary>
    public float HighBandShare { get; init; } = 0.78f;

    /// <summary>A band's thickness.</summary>
    public float BandThickness { get; init; } = 0.5f;

    /// <summary>How far a band stands out of the wall.</summary>
    public float BandDepth { get; init; } = 0.55f;

    /// <summary>The room between a piston and the machinery of its bay, on each side.</summary>
    public float BayMargin { get; init; } = 0.5f;

    /// <summary>The chance a bay holds a gear train; the others hold stacked banks.</summary>
    public float GearBayChance { get; init; } = 0.5f;

    /// <summary>Under a gear train: the chance of a low bank (else a slit of light).</summary>
    public float LowBankChance { get; init; } = 0.6f;

    /// <summary>A low bank needs a band higher than this.</summary>
    public float LowBankMinBand { get; init; } = 2.4f;

    /// <summary>A low bank's width as a share of its bay, at least...</summary>
    public float LowBankWidthShare { get; init; } = 0.5f;

    /// <summary>...and what it may add to that.</summary>
    public float LowBankWidthSpread { get; init; } = 0.35f;

    /// <summary>A bank's foot stands this far up the wall.</summary>
    public float BankFoot { get; init; } = 0.3f;

    /// <summary>How much lower than the band a low bank's top is.</summary>
    public float LowBankHeadroom { get; init; } = 0.9f;

    /// <summary>How far a bank's back stands out of the wall.</summary>
    public float BankBack { get; init; } = 0.05f;

    /// <summary>How far under the band a slit of light is.</summary>
    public float SlitBelowBand { get; init; } = 1f;

    /// <summary>How far a slit under the band stands out of the wall.</summary>
    public float SlitDepth { get; init; } = 0.25f;

    /// <summary>The slit under the band's width as a share of its bay.</summary>
    public float SlitSpanShare { get; init; } = 0.6f;

    /// <summary>The slit under the band's thickness.</summary>
    public float SlitThickness { get; init; } = 0.14f;

    /// <summary>Stacked banks: the most in a bay.</summary>
    public int MaxBanks { get; init; } = 6;

    /// <summary>Stacked banks stop when the next would start this near the wall's top.</summary>
    public float StackTopMargin { get; init; } = 1.8f;

    /// <summary>A stacked bank's least height...</summary>
    public float StackHeight { get; init; } = 2.2f;

    /// <summary>...and what it may add to that.</summary>
    public float StackHeightSpread { get; init; } = 3.2f;

    /// <summary>The room a stacked bank leaves under the wall's top.</summary>
    public float StackHeadroom { get; init; } = 0.6f;

    /// <summary>A stacked bank's width as a share of its bay, at least...</summary>
    public float StackWidthShare { get; init; } = 0.6f;

    /// <summary>...and what it may add to that.</summary>
    public float StackWidthSpread { get; init; } = 0.35f;

    /// <summary>A stacked bank is left out when its roll is under this (a gap of bare wall).</summary>
    public float StackGapBelow { get; init; } = 0.2f;

    /// <summary>A stacked bank shorter than this is left out.</summary>
    public float StackMinHeight { get; init; } = 1.4f;

    /// <summary>The chance a stacked bank has a slit of light along its wind chest.</summary>
    public float ChestSlitChance { get; init; } = 0.35f;

    /// <summary>A chest's slit's width as a share of its bank's.</summary>
    public float ChestSlitWidthShare { get; init; } = 0.7f;

    /// <summary>A chest's slit's thickness.</summary>
    public float ChestSlitThickness { get; init; } = 0.1f;

    /// <summary>How far a chest's slit stands out of its bank's front.</summary>
    public float ChestSlitInset { get; init; } = 0.03f;

    /// <summary>The gap between one stacked bank and the next, at least...</summary>
    public float StackGap { get; init; } = 0.5f;

    /// <summary>...and what it may add to that.</summary>
    public float StackGapSpread { get; init; } = 1.2f;

    /// <summary>A great gear's radius is its bay's span times this, at most...</summary>
    public float GreatSpanShare { get; init; } = 0.45f;

    /// <summary>...or the room above the band times this.</summary>
    public float GreatHeightShare { get; init; } = 0.32f;

    /// <summary>A great gear takes at least this share of that radius...</summary>
    public float GreatSizeShare { get; init; } = 0.75f;

    /// <summary>...and what it may add to that.</summary>
    public float GreatSizeSpread { get; init; } = 0.25f;

    /// <summary>A great gear's lowest edge is this far over the band.</summary>
    public float GearLift { get; init; } = 0.5f;

    /// <summary>The room a great gear keeps from the band and the wall's top, together, when placed up its range.</summary>
    public float GearRange { get; init; } = 1f;

    /// <summary>The small gear's radius as a share of the great one's, at least...</summary>
    public float SmallShare { get; init; } = 0.38f;

    /// <summary>...and what it may add to that.</summary>
    public float SmallShareSpread { get; init; } = 0.17f;

    /// <summary>The meshed gear leans off the vertical by this share of half a turn, at least...</summary>
    public float LeanShare { get; init; } = 0.12f;

    /// <summary>...and what it may add to that.</summary>
    public float LeanSpread { get; init; } = 0.2f;

    /// <summary>The small gear may stand this far outside its bay's half span.</summary>
    public float SideTolerance { get; init; } = 0.3f;

    /// <summary>The small gear's lowest edge stays this far over the band.</summary>
    public float SmallBandClear { get; init; } = 0.6f;

    /// <summary>The small gear's top stays this far under the wall's top.</summary>
    public float SmallTopClear { get; init; } = 0.3f;

    /// <summary>How far the great gear stands out of the wall.</summary>
    public float GearDepth { get; init; } = 0.3f;

    /// <summary>How far the small gear stands out of the wall (in front, so their teeth interleave).</summary>
    public float SmallGearDepth { get; init; } = 0.38f;

    /// <summary>The great gear's hub radius as a share of its radius.</summary>
    public float GreatHubShare { get; init; } = 0.12f;

    /// <summary>The small gear's hub radius as a share of its radius.</summary>
    public float SmallHubShare { get; init; } = 0.15f;

    /// <summary>The great gear's turn is this share of a tooth short of the line to the small one (a tooth on the line).</summary>
    public float GreatToothPhase { get; init; } = 0.27f;

    /// <summary>The small gear's turn is this share of a tooth short of the line back to the great one (a gap facing it).</summary>
    public float SmallToothPhase { get; init; } = 0.77f;

    /// <summary>A piston's column has this many sides.</summary>
    public int PistonSides { get; init; } = 10;

    /// <summary>A piston's plinth height.</summary>
    public float PistonPlinth { get; init; } = 0.6f;

    /// <summary>A piston's capital height.</summary>
    public float PistonCapital { get; init; } = 0.5f;

    /// <summary>A gear's thickness.</summary>
    public float GearThickness { get; init; } = 0.25f;

    /// <summary>A hub's column has this many sides.</summary>
    public int HubSides { get; init; } = 12;

    /// <summary>A hub's length along its axle.</summary>
    public float HubLength { get; init; } = 0.7f;

    /// <summary>A hub's plinth height.</summary>
    public float HubPlinth { get; init; } = 0.15f;

    /// <summary>A hub's capital height.</summary>
    public float HubCapital { get; init; } = 0.15f;
}
