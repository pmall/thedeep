using System;
using System.Collections.Generic;
using Xunit;

namespace TheDeep.Tests;

public class CaptureFilesTests
{
    private static readonly DateTime Time = new(2026, 10, 24, 9, 5, 7);

    [Fact]
    public void Stamp_is_sortable_and_file_safe()
    {
        Assert.Equal("2026-10-24_09-05-07", CaptureFiles.Stamp(Time));
    }

    [Fact]
    public void Unique_name_is_the_stamp_when_free()
    {
        Assert.Equal("2026-10-24_09-05-07", CaptureFiles.UniqueBaseName(Time, _ => false));
    }

    [Fact]
    public void Unique_name_counts_up_past_taken_names()
    {
        var taken = new HashSet<string> { "2026-10-24_09-05-07", "2026-10-24_09-05-07_2" };
        Assert.Equal("2026-10-24_09-05-07_3", CaptureFiles.UniqueBaseName(Time, taken.Contains));
    }
}
