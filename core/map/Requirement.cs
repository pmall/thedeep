using System;
using System.Collections.Generic;
using System.Linq;

namespace TheDeep;

/// <summary>
/// What a connection needs, as an expression over <see cref="Ability"/> names and item ids (keys and codes, <see cref="ItemSet"/>):
/// <c>MagGrip | Harpoon</c>, <c>Drill &amp; (Ballast | HeatPlating)</c>, <c>CryptKey</c>, <c>Drill &amp; CryptKey</c>. A name that is
/// an ability is that ability; any other is an item (the world check fails if no item of that id lies in the world, which catches a
/// misspelled ability too). <c>&amp;</c> binds tighter than <c>|</c>. Empty (or <c>None</c>) means the base moveset. Stored as
/// alternatives, each enough on its own (disjunctive normal form), which is what the map solver consumes.
/// </summary>
public sealed class Requirement
{
    /// <summary>Nothing needed.</summary>
    public static readonly Requirement None = new(new[] { Need.Nothing });

    private Requirement(IReadOnlyList<Need> options)
    {
        Options = options;
    }

    /// <summary>Alternatives: meeting any one of them is enough. Minimal and sorted.</summary>
    public IReadOnlyList<Need> Options { get; }

    /// <summary>Every item some alternative needs.</summary>
    public ItemSet Items => ItemSet.Of(Options.SelectMany(o => o.Items.Ids));

    /// <summary>Whether one of the options is met by these abilities alone, holding no item.</summary>
    public bool IsMetBy(Ability owned) => IsMetBy(owned, ItemSet.Empty);

    /// <summary>Whether one of the options is met by these abilities and these items.</summary>
    public bool IsMetBy(Ability owned, ItemSet held) => Options.Any(option => option.IsMetBy(owned, held));

    /// <summary>
    /// The requirement with its items dropped: what the body must be able to do to reach the way. An item opens a door at the way
    /// itself (a key's lock, a code's seal), so <c>Drill &amp; CryptKey</c> is reached with the Drill, and <c>CryptKey</c> with the base
    /// moveset.
    /// </summary>
    public Requirement WithoutItems() =>
        Options.All(o => o.Items.Count == 0) ? this : new Requirement(Minimize(Options.Select(o => o with { Items = ItemSet.Empty }).ToList()));

    /// <summary>Parses <paramref name="text"/>, or throws <see cref="FormatException"/> saying what is wrong and where.</summary>
    public static Requirement Parse(string text)
    {
        var parser = new Parser(text);
        List<Need> options = parser.ParseAll();
        return new Requirement(Minimize(options));
    }

    /// <summary>Parses <paramref name="text"/> without throwing: false with what is wrong and where in
    /// <paramref name="error"/>, and <see cref="None"/> as the requirement.</summary>
    public static bool TryParse(string text, out Requirement requirement, out string error)
    {
        try
        {
            requirement = Parse(text);
            error = "";
            return true;
        }
        catch (FormatException e)
        {
            requirement = None;
            error = e.Message;
            return false;
        }
    }

    /// <summary>Canonical text, e.g. <c>Drill &amp; Ballast | Drill &amp; HeatPlating</c>, abilities before items; empty when nothing is needed.</summary>
    public override string ToString() => string.Join(" | ", Options.Select(Text));

    private static string Text(Need o) =>
        string.Join(" & ", Enum.GetValues<Ability>().Where(a => a != Ability.None && o.Abilities.HasFlag(a)).Select(a => a.ToString()).Concat(o.Items.Ids));

    /// <summary>Drops alternatives that need more than another one does, then sorts.</summary>
    private static List<Need> Minimize(List<Need> options)
    {
        var distinct = options.Distinct().ToList();
        return distinct
            .Where(o => !distinct.Any(other => !other.Equals(o) && other.IsCoveredBy(o)))
            .OrderBy(o => System.Numerics.BitOperations.PopCount((uint)o.Abilities) + o.Items.Count).ThenBy(o => o.Abilities)
            .ThenBy(o => o.Items.ToString(), StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Recursive descent: or := and ('|' and)*; and := atom ('&amp;' atom)*; atom := Name | '(' or ')'.</summary>
    private sealed class Parser
    {
        private readonly string _text;
        private int _at;

        /// <summary>A parser at the start of <paramref name="text"/>; null reads as empty.</summary>
        public Parser(string text)
        {
            _text = text ?? "";
        }

        /// <summary>The whole text as alternatives, not yet minimal; empty text needs nothing. Throws
        /// <see cref="FormatException"/> on anything left unread.</summary>
        public List<Need> ParseAll()
        {
            SkipSpaces();
            if (_at == _text.Length)
            {
                return new List<Need> { Need.Nothing };
            }

            List<Need> result = ParseOr();
            SkipSpaces();
            if (_at < _text.Length)
            {
                throw Error($"unexpected '{_text[_at]}'");
            }

            return result;
        }

        private List<Need> ParseOr()
        {
            var options = ParseAnd();
            while (Accept('|'))
            {
                options.AddRange(ParseAnd());
            }

            return options;
        }

        private List<Need> ParseAnd()
        {
            List<Need> options = ParseAtom();
            while (Accept('&'))
            {
                List<Need> right = ParseAtom();
                // (a | b) & (c | d) = a&c | a&d | b&c | b&d
                options = options.SelectMany(l => right.Select(l.And)).ToList();
            }

            return options;
        }

        private List<Need> ParseAtom()
        {
            SkipSpaces();
            if (Accept('('))
            {
                List<Need> inner = ParseOr();
                if (!Accept(')'))
                {
                    throw Error("missing ')'");
                }

                return inner;
            }

            int start = _at;
            while (_at < _text.Length && (char.IsLetterOrDigit(_text[_at]) || _text[_at] == '_'))
            {
                _at++;
            }

            if (start == _at)
            {
                throw Error(_at < _text.Length ? $"expected an ability or an item, found '{_text[_at]}'" : "expected an ability or an item");
            }

            string name = _text[start.._at];
            if (!char.IsLetter(name[0]))
            {
                throw new FormatException($"'{name}' in \"{_text}\" starts with a digit (an ability or an item id starts with a letter)");
            }

            return new List<Need>
            {
                Enum.TryParse(name, ignoreCase: false, out Ability ability) && Enum.IsDefined(ability)
                    ? new Need(ability, ItemSet.Empty)
                    : new Need(Ability.None, ItemSet.Of(name)),
            };
        }

        private bool Accept(char c)
        {
            SkipSpaces();
            if (_at < _text.Length && _text[_at] == c)
            {
                _at++;
                return true;
            }

            return false;
        }

        private void SkipSpaces()
        {
            while (_at < _text.Length && char.IsWhiteSpace(_text[_at]))
            {
                _at++;
            }
        }

        private FormatException Error(string what) => new($"{what} at position {_at} in \"{_text}\"");
    }
}
