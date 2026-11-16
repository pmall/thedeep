
namespace TheDeep;

/// <summary>What reading a save's text gives: the save, or why it cannot be one (<see cref="SaveData.Parse"/>).</summary>
public abstract record SaveReading
{
    /// <summary>A save this build reads.</summary>
    public sealed record Loaded(SaveData Data) : SaveReading;

    /// <summary>A save written by a newer build (<paramref name="Found"/> is its format, <paramref name="Known"/> the newest this build reads).</summary>
    public sealed record Newer(int Found, int Known) : SaveReading;

    /// <summary>Text that is not a save, and what is wrong with it.</summary>
    public sealed record Corrupt(string Why) : SaveReading;

    /// <summary>What is wrong with a save that cannot be read, as one line for the log; empty for a loaded one.</summary>
    public string Problem => this switch
    {
        Newer n => $"it was written by a newer version of the game (save format {n.Found}, this build reads up to {n.Known})",
        Corrupt c => $"it is corrupt ({c.Why})",
        _ => "",
    };
}
