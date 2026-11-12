using System.Collections.Generic;

namespace TheDeep;

/// <summary>
/// Pure: the lines waiting to be read in one place on screen, so two things said at once are both read, one after the other. A line
/// offered while nothing shows shows at once; one offered while another shows waits its turn, unless it is the line showing or one
/// already waiting (said once is enough) or <see cref="Cap"/> lines already wait (the queue never backs up: the newest is dropped).
/// </summary>
/// <typeparam name="T">A line as the screen shows it; two equal values are the same line.</typeparam>
public sealed class LinesInTurn<T>
{
    private readonly List<T> _lines = new();

    /// <summary>A queue where at most <paramref name="cap"/> lines (at least one) wait behind the one showing.</summary>
    public LinesInTurn(int cap)
    {
        if (cap < 1)
        {
            throw new System.ArgumentOutOfRangeException(nameof(cap), cap, "at least one line waits");
        }

        Cap = cap;
    }

    /// <summary>How many lines may wait behind the one showing.</summary>
    public int Cap { get; }

    /// <summary>Whether a line is showing.</summary>
    public bool Showing => _lines.Count > 0;

    /// <summary>The line showing (only while <see cref="Showing"/>).</summary>
    public T Current => _lines.Count > 0 ? _lines[0] : throw new System.InvalidOperationException("no line is showing");

    /// <summary>How many lines wait behind the one showing.</summary>
    public int Waiting => System.Math.Max(0, _lines.Count - 1);

    /// <summary>Offers <paramref name="line"/>; true when it is to show now (nothing was showing), false when it waits or is dropped.</summary>
    public bool Offer(T line)
    {
        if (_lines.Contains(line) || _lines.Count > Cap)
        {
            return false;
        }

        _lines.Add(line);
        return _lines.Count == 1;
    }

    /// <summary>The line showing has been read: true with the next one to show, false when none waits.</summary>
    public bool Next(out T next)
    {
        if (_lines.Count > 0)
        {
            _lines.RemoveAt(0);
        }

        next = _lines.Count > 0 ? _lines[0] : default!;
        return _lines.Count > 0;
    }

    /// <summary>Nothing shows and nothing waits (a room left).</summary>
    public void Clear() => _lines.Clear();
}
