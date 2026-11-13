using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// A brick or resource whose lines the <see cref="HintLine"/> says (a locked door's line, a law's refusal). The room check holds each
/// of them to the hint's one row (<see cref="LineCheck"/>): every line it can have the hint line say is listed here.
/// </summary>
public interface IHints : INamesLines
{
    /// <summary>Each line it can have the hint line say.</summary>
    IEnumerable<StringName> HintLines { get; }
}
