namespace TheDeep;

/// <summary>
/// One of the two sides of the play plane, as the camera sees it. The values are the sign along +X, so a side serves where a
/// direction is wanted (<c>(int)side</c>) and scenes written with 1 and -1 read the same.
/// </summary>
public enum Side
{
    Left = -1,
    Right = 1,
}
