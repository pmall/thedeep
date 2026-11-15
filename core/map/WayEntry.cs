namespace TheDeep;

/// <summary>How the pilgrim came into a room, as the inside solver sees it (<see cref="WayState.Entry"/>): waking there, or through one of its exits.</summary>
public abstract record WayEntry
{
    private WayEntry()
    {
    }

    /// <summary>Waking in the start room, where the journey begins.</summary>
    public sealed record Waking : WayEntry
    {
        public override string ToString() => "the start";
    }

    /// <summary>Through the room's exit named <paramref name="Exit"/> (the arrival end of the way taken).</summary>
    public sealed record Through(string Exit) : WayEntry
    {
        public override string ToString() => Exit;
    }
}
