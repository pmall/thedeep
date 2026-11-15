namespace TheDeep;

/// <summary>
/// The sizes of a row's pieces, from its pitch and scale (shared by the layout and the meshes built for it). Each size is
/// a style spec's number (<see cref="NaveBackdropSpec"/>, <see cref="StacksBackdropSpec"/>, <see cref="FoundryBackdropSpec"/>,
/// <see cref="MachineryBackdropSpec"/>) times the row's scale or pitch.
/// </summary>
public readonly record struct BackdropDims(float Pitch, float Scale)
{
    /// <summary>The sizes of the pieces of <paramref name="row"/>.</summary>
    public static BackdropDims For(BackdropRow row) => new(row.Pitch, row.Scale);

    /// <summary>The step, meters, that the pieces built at their length (stacks, furnaces, ducts, pistons) are made in:
    /// one mesh serves every piece of a length.</summary>
    public float LengthStep => FoundryBackdropSpec.Default.LengthStep * Scale;

    /// <summary>A nave pier's height, from the row's base to where its arch springs, meters.</summary>
    public float PierHeight => NaveBackdropSpec.Default.PierHeight * Scale;

    /// <summary>A nave pier's radius, meters.</summary>
    public float PierRadius => NaveBackdropSpec.Default.PierRadius * Scale;

    /// <summary>The height of an upper tier's pier, slimmer and shorter than the first tier's, meters.</summary>
    public float UpperHeight => NaveBackdropSpec.Default.UpperHeight * Scale;

    /// <summary>How far a first-tier arch rises above its springing line, meters; over half the bay it spans, so it is
    /// pointed.</summary>
    public float PointedRise => NaveBackdropSpec.Default.PointedRiseShare * Pitch;

    /// <summary>The upper tiers' arches: pointed too (round ones stacked read as a lattice of rings), a little lower.</summary>
    public float UpperRise => NaveBackdropSpec.Default.UpperRiseShare * Pitch;

    /// <summary>The height of the stone course that runs over a tier's arches, meters.</summary>
    public float CourseHeight => NaveBackdropSpec.Default.CourseHeight * Scale;

    /// <summary>The thickness of an arch's ring, in the plane of the row, meters.</summary>
    public float ArchThickness => NaveBackdropSpec.Default.ArchThickness * Scale;

    /// <summary>An arch's depth toward the camera, meters.</summary>
    public float ArchDepth => NaveBackdropSpec.Default.ArchDepth * Scale;

    /// <summary>The side of a shaft's square section up a pier's face, meters.</summary>
    public float ShaftSection => NaveBackdropSpec.Default.ShaftSection * Scale;

    /// <summary>The height of one tier of an archive tower, meters; tiers stack up out of frame.</summary>
    public float TowerHeight => StacksBackdropSpec.Default.TowerHeight * Scale;

    /// <summary>An archive tower's width along the row, meters; the walkways span the rest of the bay.</summary>
    public float TowerWidth => StacksBackdropSpec.Default.TowerWidthShare * Pitch;

    /// <summary>A walkway's thickness, meters.</summary>
    public float WalkwayHeight => StacksBackdropSpec.Default.WalkwayHeight * Scale;

    /// <summary>A walkway's depth toward the camera, meters.</summary>
    public float WalkwayDepth => StacksBackdropSpec.Default.WalkwayDepth * Scale;

    /// <summary>A furnace's width along the row, meters.</summary>
    public float FurnaceWidth => FoundryBackdropSpec.Default.FurnaceWidthShare * Pitch;

    /// <summary>A furnace's depth toward the camera, meters.</summary>
    public float FurnaceDepth => FoundryBackdropSpec.Default.FurnaceDepth * Scale;

    /// <summary>How far a furnace rises over the room's floor, meters: each between 0.6 and 1.4 times it.</summary>
    public float FurnaceHeight => FoundryBackdropSpec.Default.FurnaceHeight * Scale;

    /// <summary>A furnace hood's width, meters.</summary>
    public float HoodWidth => FoundryBackdropSpec.Default.HoodWidthShare * Pitch;

    /// <summary>A furnace hood's height, meters.</summary>
    public float HoodHeight => FoundryBackdropSpec.Default.HoodHeight * Scale;

    /// <summary>A furnace mouth's width, meters: small, far back.</summary>
    public float MouthWidth => FoundryBackdropSpec.Default.MouthWidth * Scale;

    /// <summary>A furnace mouth's height, meters.</summary>
    public float MouthHeight => FoundryBackdropSpec.Default.MouthHeight * Scale;

    /// <summary>A stack's radius, meters; a gantry runs between two stacks' sides.</summary>
    public float StackRadius => FoundryBackdropSpec.Default.StackRadius * Scale;

    /// <summary>A duct's radius, meters.</summary>
    public float DuctRadius => FoundryBackdropSpec.Default.DuctRadius * Scale;

    /// <summary>A tank's radius, meters.</summary>
    public float TankRadius => FoundryBackdropSpec.Default.TankRadius * Scale;

    /// <summary>A tank's length, meters.</summary>
    public float TankLength => FoundryBackdropSpec.Default.TankLengthShare * Pitch;

    /// <summary>A truss's height from its lower chord to its upper one, meters; its panels are about as long.</summary>
    public float TrussDepth => FoundryBackdropSpec.Default.TrussDepth * Scale;

    /// <summary>The side of a truss chord's square section, meters; a brace is thinner.</summary>
    public float BeamSection => FoundryBackdropSpec.Default.BeamSection * Scale;

    /// <summary>A machinery gear's mean radius, meters; each gear takes between 0.75 and 1.15 times it.</summary>
    public float GearRadius => MachineryBackdropSpec.Default.GearRadiusShare * Pitch;

    /// <summary>A piston's radius, meters.</summary>
    public float PistonRadius => MachineryBackdropSpec.Default.PistonRadius * Scale;
}
