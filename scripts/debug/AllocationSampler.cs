using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Linq;

namespace TheDeep;

/// <summary>
/// What allocates, for the look-dev run's frame times: listens to the runtime's AllocationTick events (one about
/// every 100 KB allocated, naming the type that crossed the mark) while <see cref="Active"/>, and sums them by type.
/// A sample, not a count, but over a 900-frame run the top types say where the garbage comes from. Only the thread named by
/// <see cref="FollowThisThread"/> (the listener's own garbage, on its thread, would bury the rest). It costs garbage itself, so
/// it runs only when asked (THEDEEP_ALLOC_SAMPLE=1): the collections reported then are not the game's.
/// </summary>
public sealed class AllocationSampler : EventListener
{
    private const int AllocationTickId = 10;
    private const EventKeywords GcKeyword = (EventKeywords)0x1;

    private readonly ConcurrentDictionary<string, long> _bytes = new();

    private long _thread = -1;

    public bool Active { get; set; }

    /// <summary>Counts only what the calling thread allocates from now on (the main thread: game logic and physics).</summary>
    public void FollowThisThread()
    {
        // On Linux /proc/thread-self links to "<pid>/task/<tid>"; the events carry the OS thread id.
        string? link = new System.IO.DirectoryInfo("/proc/thread-self").LinkTarget;
        _thread = link is not null && long.TryParse(link[(link.LastIndexOf('/') + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out long id) ? id : -1;
    }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == "Microsoft-Windows-DotNETRuntime")
        {
            EnableEvents(eventSource, EventLevel.Verbose, GcKeyword);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (!Active || eventData.EventId != AllocationTickId || eventData.Payload is null || eventData.PayloadNames is null || (_thread >= 0 && eventData.OSThreadId != _thread))
        {
            return;
        }

        int type = eventData.PayloadNames.IndexOf("TypeName");
        int amount = eventData.PayloadNames.IndexOf("AllocationAmount64");
        if (type < 0 || amount < 0)
        {
            return;
        }

        string name = eventData.Payload[type] as string ?? "?";
        long bytes = Convert.ToInt64(eventData.Payload[amount], CultureInfo.InvariantCulture);
        _bytes.AddOrUpdate(name, bytes, (_, total) => total + bytes);
    }

    /// <summary>The types sampled most, with the KB they stand for.</summary>
    public string[] Top(int count) => _bytes.OrderByDescending(p => p.Value).Take(count)
        .Select(p => $"{p.Value / 1024} KB {p.Key}").ToArray();
}
