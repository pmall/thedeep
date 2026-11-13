using Godot;

namespace TheDeep;

/// <summary>
/// What the <see cref="Banner"/> says at once: a title and the line under it (an empty <paramref name="Line"/>: the title alone), as
/// ids of the line book. Two equal ones are the same words (said once is enough).
/// </summary>
public readonly record struct Announcement(StringName Title, StringName Line);
