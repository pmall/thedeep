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
        save.Ways.Add(new SavedWay("Hall", "East", 7, 8, "Dash", true, true));
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
}
