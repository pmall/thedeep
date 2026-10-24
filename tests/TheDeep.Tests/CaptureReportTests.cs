using Xunit;

namespace TheDeep.Tests;

public class CaptureReportTests
{
    [Fact]
    public void Aligns_values_after_the_longest_key()
    {
        string text = new CaptureReport("title")
            .Add("room", "TestRoom")
            .Add("on floor", "yes")
            .ToString();

        Assert.Equal("title\nroom      TestRoom\non floor  yes\n", text);
    }

    [Fact]
    public void Title_alone_when_empty()
    {
        Assert.Equal("title\n", new CaptureReport("title").ToString());
    }
}
