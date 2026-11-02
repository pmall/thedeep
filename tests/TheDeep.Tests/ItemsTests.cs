using System.Linq;
using Xunit;

namespace TheDeep.Tests;

public class ItemsTests
{
    private static void Two(MapGraph g, string a, string b, string both = "")
    {
        g.AddEdge(new MapEdge(a, b, b, a, Requirement.Parse(both)));
        g.AddEdge(new MapEdge(b, a, a, b, Requirement.Parse(both)));
    }

    /// <summary>A hall, a crypt with a key, and a vault behind a door the key opens; the goal beyond the vault.</summary>
    private static MapGraph Crypts()
    {
        var g = new MapGraph { Start = "Hall" };
        Two(g, "Hall", "Crypt");
        Two(g, "Hall", "Vault", "CryptKey");
        Two(g, "Vault", "End");
        g.AddItem(new MapItem("Crypt", "Key", "CryptKey"));
        g.AddGoal("End");
        return g;
    }

    [Fact]
    public void ItemSetsAreValues()
    {
        Assert.Equal(ItemSet.Of("B", "A"), ItemSet.Of("A", "B", "A"));
        Assert.Equal(ItemSet.Of("A", "B").GetHashCode(), ItemSet.Of("B", "A").GetHashCode());
        Assert.NotEqual(ItemSet.Of("A"), ItemSet.Of("A", "B"));
        Assert.Same(ItemSet.Empty, ItemSet.Of());
        Assert.True(ItemSet.Of("A").IsSubsetOf(ItemSet.Of("A", "B")));
        Assert.False(ItemSet.Of("C").IsSubsetOf(ItemSet.Of("A", "B")));
        Assert.Equal(ItemSet.Of("A", "B"), ItemSet.Of("A").Union(ItemSet.Of("B")));
        Assert.Equal("A,B", ItemSet.Of("B").With("A").ToString());
    }

    [Fact]
    public void TheKeyOpensTheVault()
    {
        MapReport r = MapSolver.Solve(Crypts());

        Assert.True(r.SoftlockFree);
        Assert.Empty(r.UnreachableRooms);
        Assert.Empty(r.UnreachableItems);
    }

    [Fact]
    public void WithoutTheKeyTheVaultStaysShut()
    {
        var g = new MapGraph { Start = "Hall" };
        Two(g, "Hall", "Vault", "CryptKey");
        g.AddGoal("Hall");

        Assert.Equal(new[] { "Vault" }, MapSolver.Solve(g).UnreachableRooms);
    }

    [Fact]
    public void AKeyBehindItsOwnDoorLeavesTheVaultUnreached()
    {
        var g = new MapGraph { Start = "Hall" };
        Two(g, "Hall", "Vault", "CryptKey");
        g.AddItem(new MapItem("Vault", "Key", "CryptKey"));
        g.AddGoal("Hall");

        MapReport r = MapSolver.Solve(g);

        Assert.Equal(new[] { "Vault" }, r.UnreachableRooms);
        Assert.Single(r.UnreachableItems);
    }

    [Fact]
    public void ADropPastTheDoorWithoutTheKeyIsASoftlock()
    {
        MapGraph g = Crypts();
        g.AddEdge(new MapEdge("Hall", "Chute", "Pit", "Top", Requirement.None)); // one way down
        g.AddEdge(new MapEdge("Pit", "Gate", "Hall", "Pit", Requirement.Parse("CryptKey")));

        MapReport r = MapSolver.Solve(g);

        Assert.Contains(r.Softlocks, s => s.State == new SolverState("Pit", Ability.None));
        Assert.DoesNotContain(r.Softlocks, s => s.State.Room == "Pit" && s.State.Held.Contains("CryptKey"));
    }

    [Fact]
    public void TheRouteTakesTheKeyFirst()
    {
        var route = MapSolver.Route(Crypts());

        Assert.NotNull(route);
        Assert.Equal(new[] { "Crypt", "Hall", "Vault", "End" }, route.Select(e => e.To));
    }

    [Fact]
    public void ARoomBehindADoorIsTriedHoldingItsKey()
    {
        Assert.Equal(ItemSet.Of("CryptKey"), MapSolver.Arrival(Crypts(), "Vault")?.Held);
        Assert.Equal(ItemSet.Empty, MapSolver.Arrival(Crypts(), "Crypt")?.Held);
    }

    [Fact]
    public void InsideTheRoomsTheKeyIsTakenWhereItIsReached()
    {
        // Every exit and every item of a room reached from any way in.
        MapGraph g = Crypts();
        RoomReach Open(WayState s) => new(s.Owned, g.ItemsIn(s.Room).Aggregate(s.Held, (h, i) => h.With(i.Item)),
            g.From(s.Room).Select(e => e.FromExit).ToList(), g.Goals.Contains(s.Room));

        InsideReport r = MapSolver.SolveInside(g, Open);

        Assert.True(r.SoftlockFree);
        Assert.Empty(r.UntakenItems);
        Assert.Empty(r.UnenteredRooms);
    }

    [Fact]
    public void AnItemNoWayNamesAndAWayNamingNoItemAreProblems()
    {
        MapGraph g = Crypts();
        g.AddItem(new MapItem("Hall", "Spare", "SpareKey"));
        Two(g, "Hall", "Shed", "Magrip"); // a misspelled ability is an item nothing gives

        var problems = g.ItemProblems(System.Array.Empty<Requirement>()).ToList();

        Assert.Contains(problems, p => p.Contains("Magrip") && p.Contains("lies in 0 rooms"));
        Assert.Contains(problems, p => p.Contains("SpareKey") && p.Contains("opens no way"));
        Assert.DoesNotContain(problems, p => p.Contains("CryptKey"));
        Assert.DoesNotContain(g.ItemProblems(new[] { Requirement.Parse("SpareKey") }), p => p.Contains("SpareKey"));
    }

    [Fact]
    public void TheInventoryHoldsForGood()
    {
        var inventory = new Inventory();
        string? taken = null;
        inventory.Taken += id => taken = id;

        Assert.True(inventory.Take("CryptKey"));
        Assert.False(inventory.Take("CryptKey"));
        Assert.Equal("CryptKey", taken);
        Assert.True(inventory.Holds("CryptKey"));
        inventory.Restore(ItemSet.Of("Other"));
        Assert.False(inventory.Holds("CryptKey"));
    }

    [Fact]
    public void AnItemLiesTakenWhenTakenOrHeld()
    {
        var p = new Pilgrimage(new SaveData());
        Assert.False(p.ItemTaken("Crypt", "Key", "CryptKey", ItemSet.Empty));
        Assert.True(p.ItemTaken("Crypt", "Key", "CryptKey", ItemSet.Of("CryptKey")));
        p.Remember("Crypt", "Key");
        Assert.True(p.ItemTaken("Crypt", "Key", "CryptKey", ItemSet.Empty));
    }

    [Fact]
    public void TheDraftLaysItemsAndComparesThem()
    {
        MapDraft d = MapDraft.Parse("start Hall\nroom Hall L\nroom Crypt L\nroom Vault L\nroom End L\nway Hall <-> Crypt\n" +
                                    "way Hall <-> Vault : CryptKey\nway Vault <-> End\nitem Crypt CryptKey\ngoal End\n");
        Assert.Equal("CryptKey", Assert.Single(d.Graph.Items).Item);
        Assert.DoesNotContain(d.Check().Problems, p => p.Contains("CryptKey"));

        var built = new MapGraph { Start = "Hall" };
        built.AddRoom("Crypt");
        Assert.Contains(d.Differences(built, System.Array.Empty<(string, Requirement)>()), p => p.Contains("an item CryptKey"));
        Assert.Throws<System.FormatException>(() => MapDraft.Parse("start A\nroom A L\nitem A Drill\n"));
    }
}
