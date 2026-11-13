using System.Collections.Generic;

namespace TheDeep;

/// <summary>
/// A brick or resource whose lines the <see cref="Banner"/> shows (a relic's name and hint, a boss's fall, a plate's scripture). The
/// room check holds each of them to the banner's box (<see cref="LineCheck"/>): every <see cref="Announcement"/> it can say is listed
/// here.
/// </summary>
public interface IAnnounces : INamesLines
{
    /// <summary>Each announcement it can have the banner say.</summary>
    IEnumerable<Announcement> Announcements { get; }
}
