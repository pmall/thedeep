using Godot;

namespace TheDeep;

/// <summary>
/// What the <see cref="Banner"/> says at once, as ids of the line book: a head and the line under it, said in a <see cref="Voice"/>.
/// Announced, the head is a title (an empty <paramref name="Line"/>: the title alone); spoken (<see cref="Spoken"/>), it is the
/// speaker's name, shown as a caption over the words. Two equal ones are the same words (said once is enough).
/// </summary>
public readonly record struct Announcement(StringName Head, StringName Line, Voice Voice = Voice.Announced)
{
    /// <summary>Words <paramref name="line"/> spoken by the one <paramref name="speaker"/> names (a line id of the book, never a raw name).</summary>
    public static Announcement Spoken(StringName speaker, StringName line) => new(speaker, line, Voice.Spoken);
}
