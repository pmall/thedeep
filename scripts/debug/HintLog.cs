using System.Collections.Generic;

namespace TheDeep;

/// <summary>The gates the pilgrim has said it can't pass, as <see cref="GateHints"/> speaks them.</summary>
public sealed class HintLog
{
    private readonly HashSet<Ability> _said = new();

    /// <summary>Listens to <paramref name="hints"/>.</summary>
    public HintLog(GateHints hints) => hints.Said += kind => _said.Add(kind);

    /// <summary>Whether the pilgrim has said it can't pass the gate of <paramref name="kind"/>.</summary>
    public bool Said(Ability kind) => _said.Contains(kind);
}
