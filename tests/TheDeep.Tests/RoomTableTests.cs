using System;
using System.IO;
using Xunit;

namespace TheDeep.Tests;

public class RoomTableTests
{
    private const string Network = """
        start A
        goal D
        room A Flooded
        room B Flooded
        room C Flooded
        room D Frozen
        way A <-> B
        way B <-> C
        hidden B C
        way A -> D : MagGrip
        relic B MagGrip
        item B Key
        boss D Warden
        ambush A
        powerup C strength
        powerup C Defense
        """;

    private const string Header = "room\tlayer\tkinds\thappens\tmachines\titems\tnotes\n";

    private const string Table = "# a comment\n" + Header +
        "A\tFlooded\tstart, Ambush\tthe trap\tVotary, Gleaner\t\n" +
        "B\tFlooded\trelic, hub\tMag-grip\t\tKey\ta note\n" +
        "C\tFlooded\tsecret, powerup\n" +
        "D\tFrozen\tgoal, boss\n";

    [Fact]
    public void ATableParsesItsColumns()
    {
        RoomTable t = RoomTable.Parse(Table);
        Assert.Equal(4, t.Rows.Count);
        RoomRow a = Assert.IsType<RoomRow>(t.Row("A"));
        Assert.Equal(new[] { RoomKind.Start, RoomKind.Ambush }, a.Kinds);
        Assert.Equal(new[] { "Votary", "Gleaner" }, a.Machines);
        Assert.Empty(a.Items);
        Assert.Equal("a note", Assert.IsType<RoomRow>(t.Row("B")).Notes);
        Assert.Equal("", Assert.IsType<RoomRow>(t.Row("C")).Happens);
    }

    [Fact]
    public void ATableAgreeingWithItsNetworkHasNoProblem()
    {
        Assert.Empty(RoomTable.Parse(Table).Check(MapDraft.Parse(Network)));
    }

    [Theory]
    [InlineData("room\tlayer\tkinds\n")]                         // a short header
    [InlineData(Header + "A\tFlooded\tcastle\n")]                // an unknown kind
    [InlineData(Header + "A\tFlooded\t\n")]                      // no kind
    [InlineData(Header + "A\tFlooded\thub\nA\tFlooded\thub\n")]  // a room twice
    [InlineData(Header + "A\tFlooded\thub\t\t\t\t\textra\n")]   // too many columns
    [InlineData("")]                                             // no header
    public void AMalformedTableIsAnError(string text)
    {
        Assert.Throws<FormatException>(() => RoomTable.Parse(text));
    }

    [Theory]
    [InlineData("D\tFrozen\tgoal, boss\n", "", "D is in the network but has no row")]
    [InlineData("D\tFrozen\tgoal, boss\n", "D\tFlooded\tgoal, boss\n", "D: the table's layer is Flooded, the network's Frozen")]
    [InlineData("B\tFlooded\trelic, hub\tMag-grip\t\tKey\ta note\n", "B\tFlooded\thub\t\t\tKey\n", "B: the network has a relic there, the table's kinds don't say relic")]
    [InlineData("D\tFrozen\tgoal, boss\n", "D\tFrozen\tgoal\n", "D: the network has a boss there, the table's kinds don't say boss")]
    [InlineData("C\tFlooded\tsecret, powerup\n", "C\tFlooded\tboss\n", "C: the table says boss, but the network has none there")]
    [InlineData("A\tFlooded\tstart, Ambush\tthe trap\tVotary, Gleaner\t\n", "A\tFlooded\tstart\n", "A: the network has an ambush there, the table's kinds don't say ambush")]
    [InlineData("B\tFlooded\trelic, hub\tMag-grip\t\tKey\ta note\n", "B\tFlooded\trelic\n", "B: the table's items are [], the network places [Key] there")]
    [InlineData("C\tFlooded\tsecret, powerup\n", "C\tFlooded\tcorridor\n", "the hidden way between B and C leads into and out of no secret room")]
    [InlineData("D\tFrozen\tgoal, boss\n", "D\tFrozen\tgoal, boss, secret\n", "D: a secret room, but the way from A is not hidden (the network's 'hidden A D')")]
    [InlineData("C\tFlooded\tsecret, powerup\n", "C\tFlooded\tsecret\n", "C: the network has a power-up there, the table's kinds don't say powerup")]
    [InlineData("D\tFrozen\tgoal, boss\n", "D\tFrozen\tgoal, boss, PowerUp\n", "D: the table says powerup, but the network has none there")]
    public void ATableDisagreeingWithItsNetworkSaysWhere(string row, string instead, string problem)
    {
        var problems = RoomTable.Parse(Table.Replace(row, instead)).Check(MapDraft.Parse(Network));
        Assert.Contains(problems, p => p.StartsWith(problem, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("prototype")]
    [InlineData("sequel")]
    public void EachWorldsTableAgreesWithItsNetwork(string world)
    {
        RoomTable t = RoomTable.Parse(File.ReadAllText(RepoPaths.Rooms(world)));
        MapDraft d = MapDraft.FromMarkdown(File.ReadAllText(RepoPaths.Network(world)));
        Assert.Empty(t.Check(d));
    }
}
