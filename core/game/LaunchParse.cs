
namespace TheDeep;

/// <summary>What reading a command line gives: the options, or the first thing wrong with it (<see cref="LaunchOptions.Parse"/>).</summary>
public abstract record LaunchParse
{
    /// <summary>A command line the game understands.</summary>
    public sealed record Parsed(LaunchOptions Options) : LaunchParse;

    /// <summary>A command line it does not, said in one line.</summary>
    public sealed record Invalid(string Message) : LaunchParse;
}
