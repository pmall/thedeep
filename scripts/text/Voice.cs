namespace TheDeep;

/// <summary>How the <see cref="Banner"/> says an <see cref="Announcement"/>: announced under a title, or spoken by someone named.</summary>
public enum Voice
{
    /// <summary>A name announced (a relic taken, a boss named or fallen, a law broken, a scripture read): its title, large.</summary>
    Announced,

    /// <summary>Words said to the pilgrim (a <see cref="Proclamation"/>): the speaker's name as a caption over them, no title.</summary>
    Spoken,
}
