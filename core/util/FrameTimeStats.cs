using System;
using System.Collections.Generic;

namespace TheDeep;

/// <summary>Average, worst and percentile frame times over a run. Pure logic, unit tested.</summary>
public sealed class FrameTimeStats
{
    private readonly List<double> _milliseconds = new();

    public int Count => _milliseconds.Count;

    public double Average { get; private set; }

    public double Worst { get; private set; }

    public void Add(double milliseconds)
    {
        _milliseconds.Add(milliseconds);
        Average += (milliseconds - Average) / _milliseconds.Count;
        Worst = Math.Max(Worst, milliseconds);
    }

    /// <summary>The frame time that <paramref name="percent"/> % of frames stay at or under (nearest rank).</summary>
    public double Percentile(double percent)
    {
        if (_milliseconds.Count == 0)
        {
            return 0.0;
        }

        var sorted = new List<double>(_milliseconds);
        sorted.Sort();
        int rank = (int)Math.Ceiling(Math.Clamp(percent, 0.0, 100.0) / 100.0 * sorted.Count);
        return sorted[Math.Max(rank, 1) - 1];
    }

    /// <summary>Frames slower than <paramref name="milliseconds"/>.</summary>
    public int CountOver(double milliseconds)
    {
        int count = 0;
        foreach (double ms in _milliseconds)
        {
            if (ms > milliseconds)
            {
                count++;
            }
        }

        return count;
    }

    public override string ToString() =>
        Count == 0 ? "no frames" : $"avg {Average:0.00} ms, p99 {Percentile(99):0.00} ms, worst {Worst:0.00} ms over {Count} frames";
}
