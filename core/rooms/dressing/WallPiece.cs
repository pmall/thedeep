namespace TheDeep;

/// <summary>The pieces a wall is dressed with; each style uses some of them.</summary>
public enum WallPiece
{
    Pilaster,
    Girder,
    Mouth,
    MouthGlow,
    Pipe,
    Chain,

    /// <summary>A bank of organ pipes on a wind chest, standing on its foot (Size: width, height; Position its foot's middle, on its back).</summary>
    Bank,

    /// <summary>A toothed gear on the wall (Size: its radius, twice; Position its middle, on its back; Roll its turn).</summary>
    Gear,

    /// <summary>An axle's end standing out of a gear (Size: its radius, twice; Position its middle, on its back).</summary>
    Hub,
    Piston,
    Slit,
    Pier,
    Shelf,
    Rack,
    Icicles,

    /// <summary>A stone band along the wall's foot (Size: length, height).</summary>
    Plinth,

    /// <summary>A stone pilaster from its foot (Size: width, height).</summary>
    Shaft,

    /// <summary>A stone block a shaft carries (Size: width, height; Position its center).</summary>
    Capital,

    /// <summary>A stone arch ring from its springing line's middle (Size: span, rise; pointed when the rise passes half the span).</summary>
    Arch,

    /// <summary>The shadowed back of a niche or a blind arch, flush on the wall (Size: width, height; Position its center).</summary>
    Recess,

    /// <summary>A stone string course along the wall (Size: length, height; Position its center).</summary>
    Course,

    /// <summary>A rose in a tympanum: a stone ring and its tracery over a recess (Size: radius, radius; Roll: the tracery's turn).</summary>
    Rose,
}
