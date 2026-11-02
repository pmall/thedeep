namespace TheDeep;

/// <summary>The pilgrim's strikes (docs/gameplay.md#fight): which one a press gives is the <see cref="Combo"/>'s rule.</summary>
public enum StrikeKind
{
    /// <summary>The first swing of a chain.</summary>
    Swing,

    /// <summary>The second, pressed while the first's chain window is open.</summary>
    Second,

    /// <summary>The third: heavier, wider, it throws back hard; the chain starts over after it.</summary>
    Finisher,

    /// <summary>In the air, holding down: a strike under the feet; what it hits bounces the pilgrim up.</summary>
    Down,

    /// <summary>During a dash: a lunge along it, longer and heavier.</summary>
    Dash,
}
