using System;
using System.Collections.Generic;
using System.Globalization;

namespace TheDeep;

/// <summary>One argument that runs a check or a tool instead of the game (<see cref="LaunchOptions.Parse"/> tells it from the others); <paramref name="TakesValue"/> if it is followed by a value (`--lookdev &lt;dir&gt;`).</summary>
public readonly record struct DriverFlag(string Name, bool TakesValue = false);
