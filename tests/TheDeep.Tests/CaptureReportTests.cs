using Xunit;

namespace TheDeep.Tests;

public class CaptureReportTests
{
    [Fact]
    public void AlignsValuesAfterTheLongestKey()
    {
        string text = new CaptureReport("title")
            .Add("room", "TestRoom")
            .Add("on floor", "yes")
            .ToString();

        Assert.Equal("title\nroom      TestRoom\non floor  yes\n", text);
    }

    [Fact]
    public void TitleAloneWhenEmpty()
    {
        Assert.Equal("title\n", new CaptureReport("title").ToString());
    }
}
