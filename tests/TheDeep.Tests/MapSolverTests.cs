using System.Collections.Generic;
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

    /// <summary>The first slice: the Nave's metal wall needs the Mag-grip found down in the Reliquary.</summary>
    private static MapGraph Slice()
    {
        var g = new MapGraph { Start = "Narthex" };
        Two(g, "Narthex", "DrownedHall");
        Two(g, "DrownedHall", "Nave");
        Two(g, "DrownedHall", "Belfry", aToB: "MagGrip | Harpoon");
        Two(g, "Nave", "Cistern", bToA: "MagGrip");
        Two(g, "Cistern", "Reliquary");
        Two(g, "Nave", "Descent", aToB: "MagGrip");
        g.AddRelic(new MapRelic("Reliquary", "MagGrip", Ability.MagGrip));
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
        (SolverState state, var path) = r.Softlocks[0];
        Assert.Equal("Pit", state.Room);
        Assert.Equal(new[] { "Narthex", "DrownedHall", "Pit" }, path.Select(p => p.Room));
    }

    [Fact]
    public void ThePitIsFineOnceItsWayOutIsGuaranteed()
    {
        MapGraph g = Slice();
        g.AddEdge(new MapEdge("DrownedHall", "Pit", "Pit", "Top", Requirement.None));
        g.AddEdge(new MapEdge("Pit", "Top", "DrownedHall", "Pit", Requirement.Parse("Drill")));
        g.AddRelic(new MapRelic("Pit", "Drill", Ability.Drill));
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
        g.AddRelic(new MapRelic("H", "Relic", Ability.Harpoon));
        g.AddRelic(new MapRelic("D", "Relic", Ability.Drill));
        g.AddRelic(new MapRelic("B", "Relic", Ability.Ballast));
        g.AddGoal("Hub");
        MapReport r = MapSolver.Solve(g);

        Assert.True(r.SoftlockFree);
        Assert.Equal(6, r.PossibleOrders);
        Assert.Equal(2, r.Orders.Count); // H D B, H B D
        Assert.All(r.Orders, o => Assert.Equal(Ability.Harpoon, o[0]));
        Assert.Equal(new[] { 1, 2 }, r.Positions[Ability.Ballast]);
    }

    [Fact]
    public void ArrivalGivesTheFewestAbilitiesARouteThereNeeds()
    {
        MapGraph g = Slice();
        Assert.Equal(Ability.None, MapSolver.Arrival(g, "Narthex")?.Owned);
        Assert.Equal(Ability.None, MapSolver.Arrival(g, "Cistern")?.Owned);
        // The Reliquary's own relic is not counted: it is still lying there on arrival.
        Assert.Equal(Ability.None, MapSolver.Arrival(g, "Reliquary")?.Owned);
        Assert.Equal(Ability.MagGrip, MapSolver.Arrival(g, "Descent")?.Owned);
        Assert.Equal(Ability.MagGrip, MapSolver.Arrival(g, "Belfry")?.Owned);
        Assert.Null(MapSolver.Arrival(g, "Nowhere")?.Owned);
    }

    [Fact]
    public void RouteTakesTheFewestWaysToTheGoalPickingUpWhatItNeeds()
    {
        IReadOnlyList<MapEdge>? route = MapSolver.Route(Slice());
        Assert.NotNull(route);
        // Down the hatch to the Reliquary for Mag-grip, back up the flue, up the metal wall to the Descent.
        Assert.Equal(new[] { "DrownedHall", "Nave", "Cistern", "Reliquary", "Cistern", "Nave", "Descent" }, route.Select(e => e.To));
        Assert.Equal("Narthex", route[0].From);
    }

    [Fact]
    public void RouteIsNullWhenTheGoalCannotBeReached()
    {
        var g = new MapGraph { Start = "A" };
        g.AddEdge(new MapEdge("A", "B", "B", "A", Requirement.Parse("Drill")));
        g.AddGoal("B");
        Assert.Null(MapSolver.Route(g));
    }

    /// <summary>A room's inside by hand: from each way in (and with what), the exits reached; relics and goals as given.</summary>
    private static System.Func<WayState, RoomReach?> Inside(System.Func<WayState, (Ability, string[], bool)> rule) => s =>
    {
        (Ability owned, string[] exits, bool goal) = rule(s);
        return new RoomReach(owned, exits, goal);
    };

    /// <summary>The slice with every room fully walkable: the Reliquary gives Mag-grip, the Descent is the goal.</summary>
    private static (Ability, string[], bool) Open(MapGraph g, WayState s) =>
        (s.Room == "Reliquary" ? s.Owned | Ability.MagGrip : s.Owned, g.From(s.Room).Select(e => e.FromExit).ToArray(), s.Room == "Descent");

    [Fact]
    public void InsideTheSliceWalkedOpenHasNoSoftlock()
    {
        MapGraph g = Slice();
        InsideReport r = MapSolver.SolveInside(g, Inside(s => Open(g, s)));
        Assert.True(r.SoftlockFree);
        Assert.Empty(r.UntakenRelics);
    }

    [Fact]
    public void AWayInTheRoomLacksIsNotTaken()
    {
        // The Nave has no way in from the Cistern after all: that way is skipped, not walked into a dead end.
        MapGraph g = Slice();
        var through = new WayEntry.Through("Cistern");
        InsideReport open = MapSolver.SolveInside(g, Inside(s => Open(g, s)));
        InsideReport r = MapSolver.SolveInside(g, s => s.Room == "Nave" && s.Entry == through ? null : Inside(t => Open(g, t))(s));
        Assert.DoesNotContain(r.Softlocks, p => p.Path.Any(s => s.Room == "Nave" && s.Entry == through));
        Assert.True(r.ReachableStates < open.ReachableStates);
    }

    [Fact]
    public void ARelicBehindAWallIsNeverTaken()
    {
        // The Reliquary's relic stands behind a wall no way in gets past: the room-level solver can't see it, the inside one can.
        MapGraph g = Slice();
        Assert.True(MapSolver.Solve(g).SoftlockFree);
        InsideReport r = MapSolver.SolveInside(g, Inside(s =>
        {
            (Ability owned, string[] exits, bool goal) = Open(g, s);
            return (s.Room == "Reliquary" ? s.Owned : owned, exits, goal);
        }));

        Assert.False(r.GoalReachable);
        Assert.Equal("Reliquary", Assert.Single(r.UntakenRelics).Room);
    }

    [Fact]
    public void AWayInThatReachesNoWayOutIsASoftlock()
    {
        // Coming into the Nave from the Cistern below (with Mag-grip), the pilgrim lands where only the Cistern's way back is reached.
        MapGraph g = Slice();
        InsideReport r = MapSolver.SolveInside(g, Inside(s =>
        {
            (Ability owned, string[] exits, bool goal) = Open(g, s);
            return s.Room == "Nave" && s.Entry == new WayEntry.Through("Cistern") ? (owned, new[] { "Cistern" }, false) : (owned, exits, goal);
        }));

        Assert.False(r.SoftlockFree);
        Assert.Contains(r.Softlocks, p => p.State == new WayState("Nave", new WayEntry.Through("Cistern"), Ability.MagGrip));
        Assert.Equal(new[] { "Narthex", "DrownedHall", "Nave", "Cistern", "Reliquary", "Cistern", "Nave" },
            r.Softlocks.First(p => p.State == new WayState("Nave", new WayEntry.Through("Cistern"), Ability.MagGrip)).Path.Select(p => p.Room));
    }

    [Fact]
    public void OrdersPreserveListedRelicOrderInOneRoomAndRespectOneWayDrops()
    {
        var g = new MapGraph { Start = "Start" };
        g.AddRelic(new MapRelic("Start", "First", Ability.MagGrip));
        Two(g, "Start", "Pair");
        g.AddRelic(new MapRelic("Pair", "Second", Ability.Drill));
        g.AddRelic(new MapRelic("Pair", "Third", Ability.Ballast));
        // A one-way drop from Start to Well that carries Harpoon and loops back only once Harpoon is held.
        g.AddEdge(new MapEdge("Start", "Drop", "Well", "Top", Requirement.None));
        g.AddEdge(new MapEdge("Well", "Out", "Start", "Return", Requirement.Parse("Harpoon")));
        g.AddRelic(new MapRelic("Well", "Zip", Ability.Harpoon));
        g.AddGoal("Start");

        MapReport r = MapSolver.Solve(g);
        Assert.True(r.SoftlockFree);
        // Either MagGrip -> Drill -> Ballast -> Harpoon or MagGrip -> Harpoon -> Drill -> Ballast.
        Assert.Equal(2, r.Orders.Count);
        Assert.All(r.Orders, o => Assert.Equal(Ability.MagGrip, o[0]));
        Assert.Contains(r.Orders, o => o.SequenceEqual(new[] { Ability.MagGrip, Ability.Drill, Ability.Ballast, Ability.Harpoon }));
        Assert.Contains(r.Orders, o => o.SequenceEqual(new[] { Ability.MagGrip, Ability.Harpoon, Ability.Drill, Ability.Ballast }));
    }

    [Fact]
    public void LargeRealGameScaleMapSolvesQuickly()
    {
        // Synthetic 150-room, 5-ability, 12-item metroidvania across 4 interconnected layers with loops, shortcuts,
        // one-way drops, and item-gated doors (Stage B/C scale stress test).
        var g = new MapGraph { Start = "L0_0" };
        const int layers = 4;
        const int perLayer = 36;
        for (int l = 0; l < layers; l++)
        {
            for (int i = 0; i < perLayer; i++)
            {
                string room = $"L{l}_{i}";
                if (i > 0)
                {
                    Two(g, $"L{l}_{i - 1}", room);
                }

                // Cross-corridor loops every 6 rooms.
                if (i >= 6 && i % 6 == 0)
                {
                    Two(g, $"L{l}_{i - 6}", room);
                }
            }

            // Two local shortcut doors + one isolated locked secret room per layer (12 items across 4 layers).
            for (int k = 0; k < 2; k++)
            {
                string item = $"Lever_{l}_{k}";
                string keyRoom = $"L{l}_{3 + k * 10}";
                string gateFrom = $"L{l}_{6 + k * 10}";
                string gateTo = $"L{l}_{10 + k * 10}";
                g.AddItem(new MapItem(keyRoom, item, item));
                Two(g, gateFrom, gateTo, aToB: item, bToA: item);
            }

            string secretKey = $"SecretKey_{l}";
            string secretRoom = $"L{l}_Secret";
            g.AddItem(new MapItem($"L{l}_22", secretKey, secretKey));
            Two(g, $"L{l}_4", secretRoom, aToB: secretKey, bToA: secretKey);
        }

        // Core goal rooms (4 * 36 + 4 secrets + 2 core = 150 rooms).
        Two(g, "L3_35", "CoreAntechamber", aToB: "MagGrip & Harpoon & Drill & Ballast & HeatPlating");
        Two(g, "CoreAntechamber", "CoreGoal");
        g.AddGoal("CoreGoal");

        // Layer links with order freedom: MagGrip or Harpoon opens L1; Drill or Ballast opens L2; HeatPlating opens L3.
        Two(g, "L0_18", "L1_0", aToB: "MagGrip | Harpoon");
        Two(g, "L0_35", "L1_18", aToB: "MagGrip | Harpoon");
        Two(g, "L1_18", "L2_0", aToB: "Drill | Ballast");
        Two(g, "L1_35", "L2_18", aToB: "Drill | Ballast");
        g.AddEdge(new MapEdge("L1_12", "Drop", "L2_6", "Top", Requirement.Parse("Drill | Ballast")));
        Two(g, "L2_35", "L3_0", aToB: "HeatPlating");
        g.AddEdge(new MapEdge("L2_18", "Chimney", "L0_30", "Top", Requirement.Parse("HeatPlating")));

        g.AddRelic(new MapRelic("L0_14", "RelicM", Ability.MagGrip));
        g.AddRelic(new MapRelic("L0_28", "RelicH", Ability.Harpoon));
        g.AddRelic(new MapRelic("L1_14", "RelicD", Ability.Drill));
        g.AddRelic(new MapRelic("L1_28", "RelicB", Ability.Ballast));
        g.AddRelic(new MapRelic("L2_24", "RelicP", Ability.HeatPlating));

        Assert.Equal(150, g.Rooms.Count);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        MapReport r = MapSolver.Solve(g);
        watch.Stop();

        Assert.True(r.SoftlockFree);
        Assert.True(r.GoalReachable);
        Assert.Empty(r.UnreachableRooms);
        Assert.Equal(32, r.Orders.Count);
        Assert.True(watch.ElapsedMilliseconds < 1000, $"150-room solve took {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void OptionalPocketOptimizationStillCatchesTrapsAndUnreachableKeys()
    {
        // A locked side branch with a one-way trap inside must still report the softlock.
        var trapped = new MapGraph { Start = "Hall" };
        Two(trapped, "Hall", "KeyRoom");
        trapped.AddItem(new MapItem("KeyRoom", "K", "SideKey"));
        Two(trapped, "Hall", "Side", aToB: "SideKey", bToA: "SideKey");
        trapped.AddEdge(new MapEdge("Side", "Drop", "Trap", "Top", Requirement.None));
        trapped.AddGoal("Hall");

        MapReport trapReport = MapSolver.Solve(trapped);
        Assert.False(trapReport.SoftlockFree);
        Assert.Contains(trapReport.Softlocks, s => s.State.Room == "Trap");

        // A key placed only past a one-way drop away from its door cannot open that door.
        var pastDrop = new MapGraph { Start = "Hall" };
        Two(pastDrop, "Hall", "Secret", aToB: "LateKey", bToA: "LateKey");
        pastDrop.AddEdge(new MapEdge("Hall", "Drop", "Below", "Top", Requirement.None));
        pastDrop.AddItem(new MapItem("Below", "K", "LateKey"));
        pastDrop.AddGoal("Below");

        MapReport dropReport = MapSolver.Solve(pastDrop);
        Assert.True(dropReport.SoftlockFree);
        Assert.Equal(new[] { "Secret" }, dropReport.UnreachableRooms);
    }
}
