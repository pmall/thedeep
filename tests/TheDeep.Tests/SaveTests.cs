using System.Linq;
using Xunit;

namespace TheDeep.Tests;

public class SaveTests
{
    [Fact]
    public void EveryFieldSurvivesARoundTrip()
    {
        var save = new SaveData
        {
            Room = "Hall",
            Shrine = "ShrineA",
            Abilities = 5,
            Strength = 2,
            Defense = 3,
        };
        save.Items.Add("CryptKey");
        save.Collected.Add(SaveData.RelicId("Hall", "Relic"));
        save.Map.Add(new SavedRoom("Hall", 1, 2, -3, -4, 5, 6));
        save.Ways.Add(new SavedWay("Hall", "East", 7, 8, "Dash", true, true, ExitSide.East));
        save.Taken.Add("Hall/East");

        SaveData? back = SaveData.FromJson(save.ToJson());

        Assert.NotNull(back);
        Assert.Equal("Hall", back.Room);
        Assert.Equal("ShrineA", back.Shrine);
        Assert.Equal(5, back.Abilities);
        Assert.Equal(2, back.Strength);
        Assert.Equal(3, back.Defense);
        Assert.Equal(save.Items, back.Items);
        Assert.Equal(save.Collected, back.Collected);
        Assert.Equal(save.Map, back.Map);
        Assert.Equal(save.Ways, back.Ways);
        Assert.Equal(save.Taken, back.Taken);
    }

    [Fact]
    public void AWaySavedWithoutItsSideIsLeftOffTheMap()
    {
        const string old = "{\"Version\":1,\"Room\":\"Hall\",\"Map\":[{\"Name\":\"Hall\",\"X\":0,\"Y\":0,\"MinX\":-2,\"MinY\":-2,\"MaxX\":60,\"MaxY\":30}],"
                           + "\"Ways\":[{\"Room\":\"Hall\",\"Exit\":\"East\",\"X\":57,\"Y\":0,\"Requires\":\"\",\"Built\":true},"
                           + "{\"Room\":\"Hall\",\"Exit\":\"Hatch\",\"X\":20,\"Y\":-1,\"Requires\":\"\",\"Built\":true,\"Side\":2}]}";

        SaveData? save = SaveData.FromJson(old);

        Assert.NotNull(save);
        Assert.Equal(2, save.Ways.Count);
        Assert.Equal("Hatch", Assert.Single(save.MapWays()).Exit);
        Assert.Equal(ExitSide.Down, save.MapWays().Single().Side);
    }

    [Fact]
    public void AnOldSaveLoadsWithTheNewerFieldsEmpty()
    {
        const string old = "{\"Version\":1,\"Room\":\"Hall\",\"Shrine\":\"\",\"Abilities\":1,\"Map\":[]}";

        SaveData? save = SaveData.FromJson(old);

        Assert.NotNull(save);
        Assert.Equal("Hall", save.Room);
        Assert.Empty(save.Items);
        Assert.Empty(save.Collected);
        Assert.Empty(save.Ways);
        Assert.Empty(save.Taken);
        Assert.Equal(0, save.Strength);
        Assert.Equal(0, save.Defense);
    }

    [Fact]
    public void TheTemporaryFileSitsBesideTheSave() =>
        Assert.Equal("user://save.json.tmp", SaveData.TempPath("user://save.json"));

    [Fact]
    public void AnUnreadableSaveIsKeptBesideTheSave() =>
        Assert.Equal("user://save.json.bad", SaveData.AsidePath("user://save.json"));

    [Fact]
    public void ANewerSaveIsReportedAsNewerWithItsVersion()
    {
        SaveReading reading = SaveData.Parse("{\"Version\":2,\"Room\":\"Hall\"}");

        var newer = Assert.IsType<SaveReading.Newer>(reading);
        Assert.Equal(2, newer.Found);
        Assert.Equal(SaveData.CurrentVersion, newer.Known);
        Assert.Contains("newer version", reading.Problem);
        Assert.DoesNotContain("corrupt", reading.Problem);
    }

    [Fact]
    public void ANewerSaveOfAnotherShapeIsStillReportedAsNewer()
    {
        SaveReading reading = SaveData.Parse("{\"Version\":9,\"Room\":\"Hall\",\"Items\":{\"a\":1}}");

        Assert.IsType<SaveReading.Newer>(reading);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("null")]
    [InlineData("[1,2]")]
    [InlineData("{\"Version\":1}")]
    [InlineData("{\"Version\":1,\"Room\":\"Hall\",\"Abilities\":\"many\"}")]
    public void ATextThatIsNoSaveIsReportedAsCorrupt(string text)
    {
        SaveReading reading = SaveData.Parse(text);

        Assert.IsType<SaveReading.Corrupt>(reading);
        Assert.Contains("corrupt", reading.Problem);
        Assert.Null(SaveData.FromJson(text));
    }

    [Fact]
    public void ASaveThisBuildReadsHasNoProblem()
    {
        SaveReading reading = SaveData.Parse(new SaveData { Room = "Hall" }.ToJson());

        Assert.IsType<SaveReading.Loaded>(reading);
        Assert.Equal("", reading.Problem);
    }
}
