using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// Holds a world's line book to every line id named for it, so none shows blank: the ids the code names (<see cref="GameLines"/>)
/// and those its content names (each <see cref="INamesLines"/> its rooms and its world reach, <see cref="ExportReach"/>), looked up
/// as the game does (the world's book, then its base). Also finds an id given twice in one book, and a letter of a line the font of
/// the world's <see cref="TextStyle"/> lacks (it would show as a box), and every banner the code and the content can say
/// (<see cref="GameLines.Announced"/>, each <see cref="IAnnounces"/>) whose words would not fit the <see cref="Banner"/>'s box
/// (<see cref="Banner.Fits"/>). Run by the room check.
/// </summary>
public sealed class LineCheck
{
    private readonly ExportReach _reach = new();

    /// <summary>Each id named, with where it was first named.</summary>
    private readonly Dictionary<StringName, string> _named = new();

    /// <summary>Each banner said, with where it was first named.</summary>
    private readonly Dictionary<Announcement, string> _announced = new();

    /// <summary>Starts with the ids and the banners the code names (<see cref="GameLines"/>).</summary>
    public LineCheck()
    {
        foreach (StringName id in GameLines.Named())
        {
            _named.TryAdd(id, "the code (GameLines)");
        }

        foreach (Announcement said in GameLines.Announced())
        {
            _announced.TryAdd(said, "the code (GameLines)");
        }
    }

    /// <summary>Adds the ids named by <paramref name="root"/>, its descendants and what they reach (a room, as authored, from <paramref name="scene"/>).</summary>
    public void Add(Node root, string scene) => Collect(_reach.From(root), scene + ": ");

    /// <summary>Adds the ids named by <paramref name="resource"/> and what it reaches (a world's definition: its items).</summary>
    public void Add(Resource resource) => Collect(_reach.From(resource), "");

    /// <summary>The problems: each named id <paramref name="book"/> lacks, each id given twice in it or in its bases.</summary>
    public List<string> Problems(LineBook book)
    {
        var problems = _named.Where(n => book.Find(n.Key) is null)
            .Select(n => $"line {n.Key} (named by {n.Value}) is in neither {book.ResourcePath} nor its base").ToList();
        for (LineBook? b = book; b is not null; b = b.Base)
        {
            string path = b.ResourcePath;
            problems.AddRange(b.Lines.GroupBy(l => l.Id).Where(g => g.Count() > 1).Select(g => $"{path}: {g.Count()} lines have the id {g.Key}"));
        }

        return problems;
    }

    /// <summary>
    /// Each banner said whose words, as <paramref name="book"/> gives them, <paramref name="banner"/> could not fit (the title on one
    /// row, the line within its rows). The rule is proven blind-free first: the longest line of the book said over and over must not fit.
    /// </summary>
    public List<string> Unfit(LineBook book, Banner banner)
    {
        var problems = new List<string>();
        string longest = "";
        for (LineBook? b = book; b is not null; b = b.Base)
        {
            foreach (TextLine line in b.Lines)
            {
                longest = line.Text.Length > longest.Length ? line.Text : longest;
            }
        }

        string tooLong = longest;
        while (tooLong.Length < 400 && longest.Length > 0)
        {
            tooLong += " " + longest;
        }

        if (banner.Fits("", tooLong))
        {
            problems.Add($"the banner's box holds a line of {tooLong.Length} letters: its fit rule cannot tell a line too long");
        }

        foreach ((Announcement said, string where) in _announced)
        {
            string title = book.Find(said.Title)?.Text ?? "";
            string line = book.Find(said.Line)?.Text ?? "";
            if (!banner.Fits(title, line))
            {
                problems.Add($"the banner {said.Title} / {said.Line} (said by {where}) does not fit its box: the title on one row, " +
                             $"the line in {banner.Rows} rows within {banner.MaxWidth} px (\"{title}\" / \"{line}\")");
            }
        }

        return problems;
    }

    /// <summary>Each letter of a line of <paramref name="book"/> or its bases (its speaker's name in capitals too) that <paramref name="font"/> lacks.</summary>
    public static List<string> MissingLetters(LineBook book, Font font)
    {
        var problems = new List<string>();
        var seen = new HashSet<char>();
        for (LineBook? b = book; b is not null; b = b.Base)
        {
            foreach (TextLine line in b.Lines)
            {
                foreach (char c in line.Text + line.Speaker + line.Speaker.ToUpperInvariant())
                {
                    if (!char.IsWhiteSpace(c) && seen.Add(c) && !font.HasChar(c))
                    {
                        problems.Add($"{b.ResourcePath}: line {line.Id} has '{c}' (U+{(int)c:X4}), which the text style's font lacks");
                    }
                }
            }
        }

        return problems;
    }

    /// <summary>Notes the ids each reached object names; a node's place is prefixed with <paramref name="scene"/> (a resource's path stands alone).</summary>
    private void Collect(List<(GodotObject Target, string Where)> reached, string scene)
    {
        foreach ((GodotObject target, string where) in reached)
        {
            if (target is IAnnounces announces)
            {
                foreach (Announcement said in announces.Announcements)
                {
                    _announced.TryAdd(said, target is Node ? scene + where : where);
                }
            }

            if (target is not INamesLines names)
            {
                continue;
            }

            foreach (StringName id in names.LineIds)
            {
                if (id != "")
                {
                    _named.TryAdd(id, target is Node ? scene + where : where);
                }
            }
        }
    }
}
