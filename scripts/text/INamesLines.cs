using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// A brick or resource that names lines of the line book (a plate's title and text, an item's name, a species' creed). The room
/// check holds every id it names to the world's book (<see cref="LineCheck"/>), so none shows blank: every export holding a line
/// id is listed here.
/// </summary>
public interface INamesLines
{
    /// <summary>The ids it names; an empty one names no line.</summary>
    IEnumerable<StringName> LineIds { get; }
}
