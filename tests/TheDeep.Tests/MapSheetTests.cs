using System.Linq;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class MapSheetTests
{
    private static readonly Vector2 Canvas = new(1600f, 900f);
    private static readonly MapPlaces Places = new(MaxScale: 6f, Margin: 96f, Baseline: 80f, Row: 24f, Clear: 48f, Gap: 8f, Mark: 18f, Spacing: 30f);

    private static ItemKind KindOf(string id) => id.EndsWith("Code", System.StringComparison.Ordinal) ? ItemKind.Code : ItemKind.Key;

    private static MapSheet Lay(MapState map, int heldRows = 0, Ability owned = Ability.None, ItemSet? held = null) =>
        MapSheet.Lay(map, Canvas, Places, heldRows, owned, held ?? ItemSet.Empty, KindOf);

    /// <summary>Two rooms side by side, their bounds overlapping 4 m at the doorway at x 40 (as rooms placed from their exits do).</summary>
    private static MapState TwoRooms()
    {
        var map = new MapState();
        map.Enter("Nave", Vector2.Zero, new Vector2(-2f, -2f), new Vector2(42f, 20f));
        map.Enter("Apse", new Vector2(40f, 0f), new Vector2(-2f, -2f), new Vector2(30f, 20f));
        map.SetWays("Nave", new[] { new MapWay("Nave", "East", new Vector2(40f, 0f), ExitSide.East, "", true) });
        map.SetWays("Apse", new[] { new MapWay("Apse", "West", new Vector2(0f, 0f), ExitSide.West, "", true) });
        map.Take("Nave", "East");
        map.Take("Apse", "West");
        return map;
    }

    [Fact]
    public void ASmallMapIsDrawnAtTheLargestScaleInTheMiddleOfItsArea()
    {
        MapSheet sheet = Lay(TwoRooms());

        Assert.Equal(6f, sheet.Scale);
        float left = sheet.Tiles.Min(t => t.Rect.MinX), right = sheet.Tiles.Max(t => t.Rect.MaxX);
        Assert.Equal((sheet.Area.MinX + sheet.Area.MaxX) * 0.5f, (left + right) * 0.5f, 0.01f);
        Assert.Empty(sheet.Problems(new[] { 50f }));
    }

    [Fact]
    public void RoomsOverlappingAtADoorwayAreCutApartByTheGap()
    {
        MapSheet sheet = Lay(TwoRooms());
        Box nave = sheet.Tiles.Single(t => t.Name == "Nave").Rect, apse = sheet.Tiles.Single(t => t.Name == "Apse").Rect;

        Assert.Equal(Places.Gap, apse.MinX - nave.MaxX, 0.01f);
        // Cut at the overlap's middle, the doorway: x 40 on the map.
        Assert.Equal(sheet.ToScreen(new Vector2(40f, 0f)).X, (nave.MaxX + apse.MinX) * 0.5f, 0.01f);
    }

    [Fact]
    public void AWayTakenIsMarkedOnBothRoomsFacingEdgesAtTheSamePoint()
    {
        MapSheet sheet = Lay(TwoRooms());
        WayMark.Taken[] taken = sheet.Marks.OfType<WayMark.Taken>().ToArray();

        Assert.Equal(2, taken.Length);
        Assert.Equal(sheet.Tiles.Single(t => t.Name == "Nave").Rect.MaxX, taken.Single(m => m.Side == ExitSide.East).At.X, 0.01f);
        Assert.Equal(sheet.Tiles.Single(t => t.Name == "Apse").Rect.MinX, taken.Single(m => m.Side == ExitSide.West).At.X, 0.01f);
        Assert.Equal(taken[0].At.Y, taken[1].At.Y, 0.01f);
        Assert.Empty(sheet.Legend);
    }

    [Fact]
    public void WaysNotTakenSayWhatThePilgrimLacksAndTheLegendNamesEachKindOnce()
    {
        MapState map = TwoRooms();
        map.SetWays("Apse", new[]
        {
            new MapWay("Apse", "West", new Vector2(0f, 0f), ExitSide.West, "", true),
            new MapWay("Apse", "Shaft", new Vector2(10f, 20f), ExitSide.Up, "Drill", true),
            new MapWay("Apse", "Crypt", new Vector2(20f, -2f), ExitSide.Down, "MagGrip & CryptKey", true),
            new MapWay("Apse", "Wall", new Vector2(30f, 5f), ExitSide.East, "MagGrip", true),
            new MapWay("Apse", "Rubble", new Vector2(30f, 10f), ExitSide.East, "", false),
            new MapWay("Apse", "Secret", new Vector2(30f, 15f), ExitSide.East, "", true, Hidden: true),
        });

        MapSheet sheet = Lay(map, owned: Ability.Drill);

        Assert.Single(sheet.Marks.OfType<WayMark.Open>());
        Assert.Single(sheet.Marks.OfType<WayMark.Unbuilt>());
        WayMark.Gated crypt = sheet.Marks.OfType<WayMark.Gated>().Single(m => m.Side == ExitSide.Down);
        Assert.Equal(new[] { Ability.MagGrip }, crypt.Abilities);
        Assert.Equal(new[] { ItemKind.Key }, crypt.Locks);
        Assert.Equal(6, sheet.Marks.Count); // the hidden way not taken has none
        Assert.Equal(new LegendKey[] { new LegendKey.Open(), new LegendKey.Gate(Ability.MagGrip), new LegendKey.Lock(ItemKind.Key), new LegendKey.Unbuilt() }, sheet.Legend);
        Assert.Empty(sheet.Problems(new[] { 120f, 40f, 40f, 160f }));
    }

    [Fact]
    public void AHiddenWayShowsOnceTaken()
    {
        MapState map = TwoRooms();
        map.SetWays("Nave", new[] { new MapWay("Nave", "East", new Vector2(40f, 0f), ExitSide.East, "", true, Hidden: true) });

        Assert.Contains(Lay(map).Marks, m => m is WayMark.Taken { Side: ExitSide.East });
    }

    [Fact]
    public void ABigMapIsScaledDownToKeepItsMarginsAndClearOfTheWordsUnderIt()
    {
        var map = new MapState();
        for (int i = 0; i < 40; i++)
        {
            map.Enter($"Room{i}", new Vector2(i * 60f, -(i % 7) * 30f), new Vector2(-2f, -2f), new Vector2(62f, 34f));
        }

        MapSheet sheet = Lay(map, heldRows: 3);

        Assert.True(sheet.Scale < Places.MaxScale);
        Assert.InRange(sheet.Tiles.Min(t => t.Rect.MinX), Places.Margin, Places.Margin + Places.Gap);
        Assert.InRange(sheet.Tiles.Max(t => t.Rect.MaxX), Canvas.X - Places.Margin - Places.Gap, Canvas.X - Places.Margin);
        Assert.True(sheet.Tiles.Max(t => t.Rect.MaxY) <= sheet.HeldBaseline(0) - Places.Clear);
        Assert.Equal(Canvas.Y - Places.Baseline, sheet.HeldBaseline(2));
        Assert.Equal(sheet.HeldBaseline(2) - 2f * Places.Row, sheet.HeldBaseline(0));
        Assert.Empty(sheet.Problems(new[] { 200f, 200f }));
    }

    [Fact]
    public void ThePilgrimsMarkStaysInItsRoomsTile()
    {
        MapSheet sheet = Lay(TwoRooms());
        Box apse = sheet.Tiles.Single(t => t.Name == "Apse").Rect;

        Vector2 inside = sheet.Pilgrim(new Vector2(10f, 5f), 4f) ?? Vector2.Zero;
        Vector2 past = sheet.Pilgrim(new Vector2(-1f, 5f), 4f) ?? Vector2.Zero;

        Assert.Equal(sheet.ToScreen(new Vector2(50f, 5f)), inside);
        Assert.Equal(apse.MinX + 4f, past.X, 0.01f);
        Assert.True(sheet.Here is { Name: "Apse", Here: true });
    }

    [Fact]
    public void AnEmptyMapHasNoTilesAndNoPilgrim()
    {
        MapSheet sheet = Lay(new MapState());

        Assert.Empty(sheet.Tiles);
        Assert.Null(sheet.Pilgrim(Vector2.Zero, 4f));
        Assert.Empty(sheet.Problems(System.Array.Empty<float>()));
    }

    [Fact]
    public void TheLegendIsOneRowCenteredAndTooWideAOneIsAProblem()
    {
        MapSheet sheet = Lay(TwoRooms());

        var starts = sheet.LegendRow(new[] { 100f, 60f });

        Assert.Equal(Canvas.X * 0.5f, (starts[0] + starts[1] + Places.Mark + 60f) * 0.5f, 0.01f);
        Assert.Equal(starts[0] + Places.Mark + 100f + Places.Spacing, starts[1], 0.01f);
        Assert.NotEmpty(sheet.Problems(new[] { 900f, 900f }));
    }

    [Fact]
    public void ItemsHeldCrowdingTheMapAreAProblem() =>
        Assert.NotEmpty(Lay(TwoRooms(), heldRows: 40).Problems(System.Array.Empty<float>()));
}
