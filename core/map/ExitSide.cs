namespace TheDeep;

/// <summary>
/// Which way an exit leads out of its room: through a doorway in its west or east wall, or a hatch in its floor (<see cref="Down"/>)
/// or in its roof (<see cref="Up"/>). The exit it leads to faces the other way.
/// </summary>
public enum ExitSide
{
    West,
    East,
    Down,
    Up,
}
