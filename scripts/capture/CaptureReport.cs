using System.Collections.Generic;
using System.Text;

namespace TheDeep;

/// <summary>
/// The small text file saved next to a capture: a title line, then aligned "key  value" lines.
/// Pure logic, unit tested.
/// </summary>
public sealed class CaptureReport
{
    private readonly string _title;
    private readonly List<(string Key, string Value)> _lines = new();

    public CaptureReport(string title)
    {
        _title = title;
    }

    public CaptureReport Add(string key, string value)
    {
        _lines.Add((key, value));
        return this;
    }

    public override string ToString()
    {
        int width = 0;
        foreach (var (key, _) in _lines)
        {
            width = System.Math.Max(width, key.Length);
        }

        var text = new StringBuilder(_title).Append('\n');
        foreach (var (key, value) in _lines)
        {
            text.Append(key.PadRight(width + 2)).Append(value).Append('\n');
        }

        return text.ToString();
    }
}
