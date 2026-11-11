using System.Linq;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class MapTests
{
    [Fact]
    public void RoomsArePlacedFromTheExitsCrossed()
    {
        // East gate at x 63 of the first room leads to a west gate at x 1 of the next: that room starts 62 m east.
        Vector2 offset = ExitMath.RoomOffset(Vector2.Zero, new Vector2(63f, 26f), new Vector2(1f, 0f));
        Assert.Equal(new Vector2(62f, 26f), offset);
    }

    [Fact]
    public void MapKeepsTheFirstPlaceOfARoom()
    {
        var map = new MapState();
        map.Enter("A", Vector2.Zero, new Vector2(-2f, -2f), new Vector2(60f, 30f));
        map.Enter("B", new Vector2(62f, 0f), Vector2.Zero, new Vector2(40f, 20f));
        MapRoom again = map.Enter("A", new Vector2(5f, 5f), new Vector2(-2f, -2f), new Vector2(60f, 30f));

        Assert.Equal(Vector2.Zero, again.Offset);
        Assert.Equal("A", map.Current);
        Assert.Equal(2, map.Rooms.Count);
        Assert.Equal(new Vector2(102f, 20f), map.Find("B")?.MapMax);
    }

    [Fact]
    public void WaysAreKeptWithTheSaveAndTakenOnesRemembered()
    {
        var map = new MapState();
        map.Enter("Nave", System.Numerics.Vector2.Zero, new System.Numerics.Vector2(-2, -2), new System.Numerics.Vector2(40, 20));
        map.SetWays("Nave", new[]
        {
            new MapWay("Nave", "West", new System.Numerics.Vector2(0, 0), "", true),
            new MapWay("Nave", "Wall", new System.Numerics.Vector2(30, 12), "MagGrip", true),
        });
        map.Take("Nave", "West");
        Assert.True(map.Taken("Nave", "West"));
        Assert.False(map.Taken("Nave", "Wall"));

        var data = new SaveData { Room = "Nave" };
        data.SetMap(map);
        SaveData? back = SaveData.FromJson(data.ToJson());
        Assert.NotNull(back);
        var restored = new MapState();
        restored.Restore(back.MapRooms(), "Nave", back.MapWays(), back.Taken);
        Assert.Equal(2, restored.WaysOf("Nave").Count);
        Assert.Equal("MagGrip", restored.WaysOf("Nave").Single(w => w.Exit == "Wall").Requires);
        Assert.True(restored.Taken("Nave", "West"));
        Assert.False(restored.Taken("Nave", "Wall"));
    }
}
