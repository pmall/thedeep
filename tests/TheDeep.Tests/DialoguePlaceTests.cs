using Xunit;

namespace TheDeep.Tests;

public class DialoguePlaceTests
{
    private static readonly Box High = new(450f, 96f, 1150f, 230f);
    private static readonly Box Low = new(450f, 640f, 1150f, 774f);

    [Fact]
    public void TheBoxStandsHighByDefault()
    {
        Assert.False(DialoguePlace.Low(High, Low, new(780f, 500f, 820f, 620f), new(1000f, 350f, 1060f, 420f)));
    }

    [Fact]
    public void ASpeakerHighOnScreenSendsItLow()
    {
        Assert.True(DialoguePlace.Low(High, Low, new(780f, 500f, 820f, 620f), new(900f, 150f, 960f, 210f)));
    }

    [Fact]
    public void ThePilgrimHighOnScreenSendsItLow()
    {
        Assert.True(DialoguePlace.Low(High, Low, new(780f, 120f, 820f, 240f), new(1300f, 400f, 1360f, 460f)));
    }

    [Fact]
    public void WhenBothPlacesCoverSomeoneThePilgrimStaysClear()
    {
        Assert.False(DialoguePlace.Low(High, Low, new(780f, 600f, 820f, 720f), new(900f, 150f, 960f, 210f)));
        Assert.True(DialoguePlace.Low(High, Low, new(780f, 120f, 820f, 240f), new(900f, 650f, 960f, 700f)));
    }
}
