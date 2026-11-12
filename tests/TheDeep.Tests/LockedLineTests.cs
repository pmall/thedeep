using Xunit;

namespace TheDeep.Tests;

public class LockedLineTests
{
    [Fact]
    public void LockedLineNamesTheItemAndItsRoom()
    {
        Assert.Empty(LockedLine.Unnamed("The litany of the pyre lies in the Belfry.", "The litany of the pyre", "Belfry"));
        Assert.Equal(new[] { "Belfry" }, LockedLine.Unnamed("Sealed. The litany of the pyre opens it.", "The litany of the pyre", "Belfry"));
        Assert.Equal(new[] { "sacristy key" }, LockedLine.Unnamed("Locked. Its key is in the Scriptorium.", "The sacristy key", "Scriptorium"));
        Assert.Equal("Drowned Hall", LockedLine.Words("DrownedHall"));
    }
}
