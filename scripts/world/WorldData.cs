namespace TheDeep;

/// <summary>
/// What reading one of a world's data files gives (<see cref="WorldDefinition.LoadNetwork"/>, <see cref="WorldDefinition.LoadRoomTable"/>):
/// the data, or one of the two failures a world's author can cause, for the caller to report in its own words.
/// </summary>
public abstract record WorldData<T>
{
    private WorldData()
    {
    }

    /// <summary>The file was read and parsed.</summary>
    public sealed record Loaded(T Value) : WorldData<T>;

    /// <summary>The world names no such file, or the file it names does not exist.</summary>
    public sealed record Missing : WorldData<T>;

    /// <summary>The file does not parse; <paramref name="Message"/> is the parser's, naming the file's line.</summary>
    public sealed record Malformed(string Message) : WorldData<T>;
}
