namespace TheDeep;

/// <summary>
/// The numbers of a foundry backdrop (<see cref="FoundryBackdrop"/>): sizes in meters per unit of the row's scale (a share
/// is of the row's pitch, a span or a length, as its name says), chances 0 to 1.
/// </summary>
public sealed record FoundryBackdropSpec
{
    /// <summary>The approved look.</summary>
    public static readonly FoundryBackdropSpec Default = new();

    /// <summary>The step that the pieces built at their length (stacks, furnaces, ducts, pistons) are made in: one mesh serves every piece of a length.</summary>
    public float LengthStep { get; init; } = 2f;

    /// <summary>A furnace's width along the row, as a share of the pitch.</summary>
    public float FurnaceWidthShare { get; init; } = 0.6f;

    /// <summary>A furnace's depth toward the camera.</summary>
    public float FurnaceDepth { get; init; } = 4f;

    /// <summary>How far a furnace rises over the room's floor, on average.</summary>
    public float FurnaceHeight { get; init; } = 6f;

    /// <summary>A furnace rises at least this share of <see cref="FurnaceHeight"/>...</summary>
    public float FurnaceHeightShare { get; init; } = 0.6f;

    /// <summary>...and what it may add to that.</summary>
    public float FurnaceHeightSpread { get; init; } = 0.8f;

    /// <summary>A furnace hood's width, as a share of the pitch.</summary>
    public float HoodWidthShare { get; init; } = 0.36f;

    /// <summary>A furnace hood's height.</summary>
    public float HoodHeight { get; init; } = 2.4f;

    /// <summary>A furnace mouth's width: small, far back.</summary>
    public float MouthWidth { get; init; } = 1.4f;

    /// <summary>A furnace mouth's height.</summary>
    public float MouthHeight { get; init; } = 0.8f;

    /// <summary>A stack's radius; a gantry runs between two stacks' sides.</summary>
    public float StackRadius { get; init; } = 1.1f;

    /// <summary>A duct's radius.</summary>
    public float DuctRadius { get; init; } = 0.5f;

    /// <summary>A tank's radius.</summary>
    public float TankRadius { get; init; } = 1.5f;

    /// <summary>A tank's length, as a share of the pitch.</summary>
    public float TankLengthShare { get; init; } = 0.5f;

    /// <summary>A truss's height from its lower chord to its upper one; its panels are about as long.</summary>
    public float TrussDepth { get; init; } = 1.8f;

    /// <summary>The side of a truss chord's square section; a brace is thinner.</summary>
    public float BeamSection { get; init; } = 0.3f;

    /// <summary>The chance of a furnace on a bay line that is not an even one.</summary>
    public float ExtraFurnaceChance { get; init; } = 0.25f;

    /// <summary>The chance a stack stops short instead of running out of frame.</summary>
    public float ShortStackChance { get; init; } = 0.25f;

    /// <summary>A short stack rises at least this share of the room's band over its hood...</summary>
    public float ShortStackShare { get; init; } = 0.5f;

    /// <summary>...and what it may add to that.</summary>
    public float ShortStackSpread { get; init; } = 0.4f;

    /// <summary>The chance a furnace of a glowing row has its mouth lit.</summary>
    public float MouthChance { get; init; } = 0.4f;

    /// <summary>How far the mouth stands out on the furnace's front, as a share of its depth.</summary>
    public float MouthDepthShare { get; init; } = 0.5f;

    /// <summary>How far under its furnace's top the mouth sits, as a share of <see cref="FurnaceHeight"/>.</summary>
    public float MouthDropShare { get; init; } = 0.4f;

    /// <summary>A gantry stands this far over its hoods, at least...</summary>
    public float GantryLift { get; init; } = 2f;

    /// <summary>...and what it may add to that.</summary>
    public float GantryLiftSpread { get; init; } = 4f;

    /// <summary>The chance a gantry is one level (else two).</summary>
    public float OneLevelChance { get; init; } = 0.5f;

    /// <summary>The chance a tank lies on a gantry wide enough.</summary>
    public float TankChance { get; init; } = 0.6f;

    /// <summary>A gantry must be this many tank lengths wide to carry a tank.</summary>
    public float TankFit { get; init; } = 1.4f;

    /// <summary>A tank stays this many tank lengths from the ends of its range.</summary>
    public float TankMargin { get; init; } = 1.2f;

    /// <summary>The chance a truss panel has a chain hanging.</summary>
    public float ChainChance { get; init; } = 0.12f;

    /// <summary>A chain hangs at least this share of the way down to the room's floor...</summary>
    public float ChainLengthShare { get; init; } = 0.2f;

    /// <summary>...and what it may add to that.</summary>
    public float ChainLengthSpread { get; init; } = 0.4f;

    /// <summary>The chance a duct runs between two stacks over their gantry.</summary>
    public float DuctChance { get; init; } = 0.5f;

    /// <summary>A duct runs this far over the top gantry, at least...</summary>
    public float DuctLift { get; init; } = 3f;

    /// <summary>...and what it may add to that.</summary>
    public float DuctLiftSpread { get; init; } = 3f;

    /// <summary>A furnace's bevel, times the row's scale.</summary>
    public float FurnaceBevel { get; init; } = 0.2f;

    /// <summary>A hood's depth as a share of a furnace's.</summary>
    public float HoodDepthShare { get; init; } = 0.7f;

    /// <summary>A hood's bevel, times the row's scale.</summary>
    public float HoodBevel { get; init; } = 0.15f;

    /// <summary>A furnace mouth's depth, times the row's scale.</summary>
    public float MouthThickness { get; init; } = 0.3f;

    /// <summary>A furnace mouth's bevel, times the row's scale.</summary>
    public float MouthBevel { get; init; } = 0.05f;

    /// <summary>A stack's flanges stand this far apart, times the row's scale.</summary>
    public float StackFlange { get; init; } = 3.5f;

    /// <summary>A duct's flanges stand this far apart, times the row's scale.</summary>
    public float DuctFlange { get; init; } = 2.5f;

    /// <summary>A tank's flanges stand this far apart, times the row's scale.</summary>
    public float TankFlange { get; init; } = 1.6f;

    /// <summary>A truss member's bevel, meters.</summary>
    public float BeamBevel { get; init; } = 0.02f;

    /// <summary>A brace's section as a share of a chord's.</summary>
    public float BraceShare { get; init; } = 0.7f;

    /// <summary>A chain's radius, times the row's scale.</summary>
    public float ChainRadius { get; init; } = 0.08f;
}
