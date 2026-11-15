namespace TheDeep;

/// <summary>The pieces a backdrop row is built from; each style uses some of them.</summary>
public enum BackdropPiece
{
    Pier,
    UpperPier,
    PointedArch,
    UpperArch,
    Course,

    /// <summary>A slender shaft running up a nave pier's face from its springing to its top tier.</summary>
    Shaft,
    Tower,
    Walkway,
    Icicles,

    /// <summary>A furnace's body, from the row's foot up past the room's floor.</summary>
    Furnace,

    /// <summary>The narrower hood on a furnace that its stack rises from.</summary>
    Hood,

    /// <summary>A furnace's mouth on its front face: the only light of the foundry's backdrop.</summary>
    FurnaceMouth,

    /// <summary>A banded stack rising from a hood.</summary>
    Stack,

    /// <summary>A horizontal duct from one stack to the next.</summary>
    Duct,

    /// <summary>A banded tank lying on a gantry.</summary>
    Tank,
    Chord,
    Brace,
    Chain,

    /// <summary>A great toothed gear facing the camera, its back on its origin's plane; its length is its radius.</summary>
    Gear,
    Hub,
    Piston,
}
