namespace TheDeep;

/// <summary>
/// A pocket: a stretch of ground from <see cref="MinX"/> to <see cref="MaxX"/> (the capsule's center) on solid <see cref="Ground"/>'s
/// top at <see cref="Y"/> (and any ground flush with it), every place of which is under a solid that cuts a jump short
/// (<see cref="GroundRules.Headroom"/>); <see cref="Ceiling"/> is the lowest unmarked solid over it, <see cref="Clearance"/> meters over the ground.
/// </summary>
public readonly record struct LowCeiling(int Ground, int Ceiling, float Y, float MinX, float MaxX, float Clearance);
