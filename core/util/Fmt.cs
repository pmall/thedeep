using System.Globalization;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// Numbers and places as the checks' messages and reports write them (<c>instructions/principles.md#invariant-culture</c>):
/// two decimals at most, always with a point, whatever the machine's locale.
/// </summary>
public static class Fmt
{
    /// <summary>A number, at most two decimals.</summary>
    public static string Num(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>A point as "(x, y)".</summary>
    public static string Point(Vector2 v) => $"({Num(v.X)}, {Num(v.Y)})";

    /// <summary>A box as "(minX, minY)..(maxX, maxY)".</summary>
    public static string Rect(Box b) => $"{Point(new Vector2(b.MinX, b.MinY))}..{Point(new Vector2(b.MaxX, b.MaxY))}";
}
