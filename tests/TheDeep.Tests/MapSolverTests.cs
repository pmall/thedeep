using System.Linq;
using Xunit;

namespace TheDeep.Tests;

public class MapSolverTests
{
    private static void Two(MapGraph g, string a, string b, string aToB = "", string bToA = "")
    {
        g.AddEdge(new MapEdge(a, b, b, a, Requirement.Parse(aToB)));
        g.AddEdge(new MapEdge(b, a, a, b, Requirement.Parse(bToA)));
    }

    /// <summary>The M6 slice: the Nave's metal wall needs the Mag-grip found down in the Reliquary.</summary>
    private static MapGraph Slice()
    {
        var g = new MapGraph { Start = "Narthex" };
        Two(g, "Narthex", "DrownedHall");
        Two(g, "DrownedHall", "Nave");
        Two(g, "DrownedHall", "Belfry", aToB: "MagGrip | Harpoon");
        Two(g, "Nave", "Cistern", bToA: "MagGrip");
        Two(g, "Cistern", "Reliquary");
        Two(g, "Nave", "Descent", aToB: "MagGrip");
        g.AddItem(new MapItem("Reliquary", "MagGrip", Ability.MagGrip));
        g.AddGoal("Descent");
        return g;
    }

    [Fact]
    public void TheSliceHasNoSoftlock()
    {
        MapReport r = MapSolver.Solve(Slice());
        Assert.True(r.SoftlockFree);
        Assert.Empty(r.UnreachableRooms);
        Assert.Single(r.Orders);
        Assert.Equal(1, r.PossibleOrders);
    }

    [Fact]
    public void AOneWayPitWithoutTheWayOutIsASoftlock()
    {
        MapGraph g = Slice();
        // A drop from the hall into a pit whose only way out needs a Drill nobody can find.
        g.AddEdge(new MapEdge("DrownedHall", "Pit", "Pit", "Top", Requirement.None));
        g.AddEdge(new MapEdge("Pit", "Top", "DrownedHall", "Pit", Requirement.Parse("Drill")));
        MapReport r = MapSolver.Solve(g);

        Assert.False(r.SoftlockFree);
        (SolverState state, var path) = r.Softlocks.First();
        Assert.Equal("Pit", state.Room);
        Assert.Equal(new[] { "Narthex", "DrownedHall", "Pit" }, path.Select(p => p.Room));
    }

    [Fact]
    public void ThePitIsFineOnceItsWayOutIsGuaranteed()
    {
        MapGraph g = Slice();
        g.AddEdge(new MapEdge("DrownedHall", "Pit", "Pit", "Top", Requirement.None));
        g.AddEdge(new MapEdge("Pit", "Top", "DrownedHall", "Pit", Requirement.Parse("Drill")));
        g.AddItem(new MapItem("Pit", "Drill", Ability.Drill));
        MapReport r = MapSolver.Solve(g);

        Assert.True(r.SoftlockFree);
        Assert.Equal(2, r.Orders.Count); // Drill then Mag-grip, or Mag-grip then Drill
        Assert.Equal(new[] { 0, 1 }, r.Positions[Ability.Drill]);
    }

    [Fact]
    public void AnUnreachableGoalIsReported()
    {
        var g = new MapGraph { Start = "A" };
        Two(g, "A", "B", aToB: "Ballast");
        g.AddGoal("B");
        MapReport r = MapSolver.Solve(g);

        Assert.False(r.GoalReachable);
        Assert.False(r.SoftlockFree);
        Assert.Equal(new[] { "B" }, r.UnreachableRooms);
    }

    [Fact]
    public void OrdersFollowTheGates()
    {
        // A hub with three relics: Harpoon behind nothing, Drill behind Harpoon, Ballast behind Drill or Harpoon.
        var g = new MapGraph { Start = "Hub" };
        Two(g, "Hub", "H");
        Two(g, "Hub", "D", aToB: "Harpoon");
        Two(g, "Hub", "B", aToB: "Drill | Harpoon");
        g.AddItem(new MapItem("H", "Relic", Ability.Harpoon));
        g.AddItem(new MapItem("D", "Relic", Ability.Drill));
        g.AddItem(new MapItem("B", "Relic", Ability.Ballast));
        g.AddGoal("Hub");
        MapReport r = MapSolver.Solve(g);

        Assert.True(r.SoftlockFree);
        Assert.Equal(6, r.PossibleOrders);
        Assert.Equal(2, r.Orders.Count); // H D B, H B D
        Assert.All(r.Orders, o => Assert.Equal(Ability.Harpoon, o[0]));
        Assert.Equal(new[] { 1, 2 }, r.Positions[Ability.Ballast]);
    }
}
