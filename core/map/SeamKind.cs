namespace TheDeep;

/// <summary>The kinds of meeting between two solids that the physics or the renderer gets wrong.</summary>
public enum SeamKind
{
    /// <summary>Two tops side by side, one higher by less than the ledge assist (and more than nothing): a bump the run trips on.</summary>
    Step,

    /// <summary>Two solids overlapping with a face in the same plane: the faces fight for the same depth (z-fighting).</summary>
    Coplanar,
}
