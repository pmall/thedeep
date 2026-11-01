using System;
using System.Collections.Generic;
using System.Linq;

namespace TheDeep;

/// <summary>
/// What a connection needs, as an expression over <see cref="Ability"/> names: <c>MagGrip | Harpoon</c>,
/// <c>Drill &amp; (Ballast | HeatPlating)</c>. <c>&amp;</c> binds tighter than <c>|</c>. Empty (or
/// <c>None</c>) means the base moveset. Stored as alternatives, each a set of abilities that is enough
/// on its own (disjunctive normal form), which is what the map solver (M7) consumes.
/// </summary>
public sealed class Requirement
{
    /// <summary>Nothing needed.</summary>
    public static readonly Requirement None = new(new[] { Ability.None });

    private Requirement(IReadOnlyList<Ability> options)
    {
        Options = options;
    }

    /// <summary>Alternatives: owning every ability of any one of them is enough. Minimal and sorted.</summary>
    public IReadOnlyList<Ability> Options { get; }

    public bool IsMetBy(Ability owned) => Options.Any(option => (owned & option) == option);

    /// <summary>Parses <paramref name="text"/>, or throws <see cref="FormatException"/> saying what is wrong and where.</summary>
    public static Requirement Parse(string text)
    {
        var parser = new Parser(text);
        List<Ability> options = parser.ParseAll();
        return new Requirement(Minimize(options));
    }

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

    /// <summary>Canonical text, e.g. <c>Drill &amp; Ballast | Drill &amp; HeatPlating</c>; empty when nothing is needed.</summary>
    public override string ToString() =>
        string.Join(" | ", Options.Select(o => string.Join(" & ", Enum.GetValues<Ability>().Where(a => a != Ability.None && o.HasFlag(a)))));

    /// <summary>Drops alternatives that need more than another one does, then sorts.</summary>
    private static List<Ability> Minimize(List<Ability> options)
    {
        var distinct = options.Distinct().ToList();
        return distinct
            .Where(o => !distinct.Any(other => other != o && (o & other) == other))
            .OrderBy(o => System.Numerics.BitOperations.PopCount((uint)o)).ThenBy(o => o)
            .ToList();
    }

    /// <summary>Recursive descent: or := and ('|' and)*; and := atom ('&amp;' atom)*; atom := Name | '(' or ')'.</summary>
    private sealed class Parser
    {
        private readonly string _text;
        private int _at;

        public Parser(string text)
        {
            _text = text ?? "";
        }

        public List<Ability> ParseAll()
        {
            SkipSpaces();
            if (_at == _text.Length)
            {
                return new List<Ability> { Ability.None };
            }

            List<Ability> result = ParseOr();
            SkipSpaces();
            if (_at < _text.Length)
            {
                throw Error($"unexpected '{_text[_at]}'");
            }

            return result;
        }

        private List<Ability> ParseOr()
        {
            var options = ParseAnd();
            while (Accept('|'))
            {
                options.AddRange(ParseAnd());
            }

            return options;
        }

        private List<Ability> ParseAnd()
        {
            List<Ability> options = ParseAtom();
            while (Accept('&'))
            {
                List<Ability> right = ParseAtom();
                // (a | b) & (c | d) = a&c | a&d | b&c | b&d
                options = options.SelectMany(l => right.Select(r => l | r)).ToList();
            }

            return options;
        }

        private List<Ability> ParseAtom()
        {
            SkipSpaces();
            if (Accept('('))
            {
                List<Ability> inner = ParseOr();
                if (!Accept(')'))
                {
                    throw Error("missing ')'");
                }

                return inner;
            }

            int start = _at;
            while (_at < _text.Length && char.IsLetter(_text[_at]))
            {
                _at++;
            }

            if (start == _at)
            {
                throw Error(_at < _text.Length ? $"expected an ability, found '{_text[_at]}'" : "expected an ability");
            }

            string name = _text[start.._at];
            if (!Enum.TryParse(name, ignoreCase: false, out Ability ability) || !Enum.IsDefined(ability))
            {
                string known = string.Join(", ", Enum.GetNames<Ability>());
                throw new FormatException($"unknown ability '{name}' in \"{_text}\" (known: {known})");
            }

            return new List<Ability> { ability };
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
