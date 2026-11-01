namespace TheDeep;

/// <summary>
/// One tick of the devices as the player holds them, before the controls read it: the stick (or keys) on each axis, -1 to 1, the
/// jump held, and the presses that began this tick.
/// </summary>
public readonly record struct RawInput(float AxisX, float AxisY, bool JumpHeld, bool JumpPressed, bool DashPressed, bool HarpoonPressed);
