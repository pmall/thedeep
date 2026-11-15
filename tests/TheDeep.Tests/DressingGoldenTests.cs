using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

/// <summary>
/// Pins the placements every wall and backdrop style makes for fixed rooms (an approved look): a refactor of the layouts
/// must leave each hash as it is, and a deliberate change of the look updates them here.
/// </summary>
public class DressingGoldenTests
{
    private const ulong Basis = 14695981039346656037UL;

    private static readonly BackdropRow[] Rows =
    [
        new(-60f, 60f, -30f, 50f, -10f, 20f, 9f, 1f, 11),
        new(-80f, 90f, -40f, 60f, -10f, 25f, 12f, 1.3f, 29, Glows: true),
        new(-100f, 100f, -50f, 70f, -12f, 30f, 15f, 1.7f, 3, Glows: true),
    ];

    private static readonly (float Width, float Height, int Seed, float Frost)[] Walls =
    [
        (44f, 24f, 5, 0f), (70f, 24f, 3, 0.5f), (30f, 12f, 9, 1f), (100f, 40f, 21, 0.3f), (8f, 6f, 2, 0f),
    ];

    private static readonly Dictionary<string, string> Expected = new()
    {
        ["wall Foundry"] = "8DA951E4D0E5230B",
        ["wall Machinery"] = "A8BB40AC5E1813F9",
        ["wall Archive"] = "B81256F79AD93456",
        ["wall Nave"] = "774CD520527853CA",
        ["backdrop Nave"] = "3971902BDF24ABF9",
        ["backdrop Stacks"] = "4B1D9EA917D93228",
        ["backdrop Foundry"] = "A471E56E845A1325",
        ["backdrop Machinery"] = "334EDE5091EFF296",
    };

    [Theory]
    [InlineData(WallStyle.Foundry)]
    [InlineData(WallStyle.Machinery)]
    [InlineData(WallStyle.Archive)]
    [InlineData(WallStyle.Nave)]
    public void WallStylesKeepTheirPlacements(WallStyle style)
    {
        ulong h = Basis;
        foreach ((float width, float height, int seed, float frost) in Walls)
        {
            foreach (WallPlacement p in WallLayout.Build(style, width, height, seed, frost))
            {
                h = Mix(h, (int)p.Piece, p.Position, p.Size.X, p.Size.Y, p.Roll + p.Variant);
            }
        }

        Assert.Equal(Expected[$"wall {style}"], $"{h:X16}");
    }

    [Theory]
    [InlineData(BackdropStyle.Nave)]
    [InlineData(BackdropStyle.Stacks)]
    [InlineData(BackdropStyle.Foundry)]
    [InlineData(BackdropStyle.Machinery)]
    public void BackdropStylesKeepTheirPlacements(BackdropStyle style)
    {
        ulong h = Basis;
        foreach (BackdropRow row in Rows)
        {
            foreach (BackdropPlacement p in BackdropLayout.Build(style, row))
            {
                h = Mix(h, (int)p.Piece, p.Position, p.RollZ, p.PitchX, p.Length);
            }
        }

        Assert.Equal(Expected[$"backdrop {style}"], $"{h:X16}");
    }

    private static ulong Mix(ulong h, int piece, Vector3 v, float a, float b, float c)
    {
        h = Step(h, (ulong)piece);
        h = Step(h, (uint)BitConverter.SingleToInt32Bits(v.X));
        h = Step(h, (uint)BitConverter.SingleToInt32Bits(v.Y));
        h = Step(h, (uint)BitConverter.SingleToInt32Bits(v.Z));
        h = Step(h, (uint)BitConverter.SingleToInt32Bits(a));
        h = Step(h, (uint)BitConverter.SingleToInt32Bits(b));
        return Step(h, (uint)BitConverter.SingleToInt32Bits(c));
    }

    private static ulong Step(ulong h, ulong value) => unchecked((h ^ value) * 1099511628211UL);
}
