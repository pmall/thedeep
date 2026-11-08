using System;
using System.Collections.Generic;
using Xunit;

namespace TheDeep.Tests;

public class CaptureFilesTests
{
    private static readonly DateTime Time = new(2026, 10, 24, 9, 5, 7);

    [Fact]
    public void StampIsSortableAndFileSafe()
    {
        Assert.Equal("2026-10-24_09-05-07", CaptureFiles.Stamp(Time));
    }

    [Fact]
    public void UniqueNameIsTheStampWhenFree()
    {
        Assert.Equal("2026-10-24_09-05-07", CaptureFiles.UniqueBaseName(Time, _ => false));
    }

    [Fact]
    public void UniqueNameCountsUpPastTakenNames()
    {
        var taken = new HashSet<string> { "2026-10-24_09-05-07", "2026-10-24_09-05-07_2" };
        Assert.Equal("2026-10-24_09-05-07_3", CaptureFiles.UniqueBaseName(Time, taken.Contains));
    }
}
