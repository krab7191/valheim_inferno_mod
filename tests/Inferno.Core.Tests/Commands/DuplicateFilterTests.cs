using System;
using Inferno.Core.Commands;
using Xunit;

namespace Inferno.Core.Tests.Commands;

public class DuplicateFilterTests
{
    [Fact]
    public void CopiesWithinWindow_AreRejected()
    {
        var filter = new DuplicateFilter(2.0);

        Assert.True(filter.IsFirst(1, "!fires list", 10.0));
        Assert.False(filter.IsFirst(1, "!fires list", 10.1));
        Assert.False(filter.IsFirst(1, "!fires list", 11.9));
    }

    [Fact]
    public void SameTextAfterWindow_IsNew()
    {
        var filter = new DuplicateFilter(2.0);

        Assert.True(filter.IsFirst(1, "!fires list", 10.0));
        Assert.True(filter.IsFirst(1, "!fires list", 12.0));
    }

    [Fact]
    public void DifferentSenderOrText_IsNew()
    {
        var filter = new DuplicateFilter(2.0);

        Assert.True(filter.IsFirst(1, "!fires list", 10.0));
        Assert.True(filter.IsFirst(2, "!fires list", 10.0));
        Assert.True(filter.IsFirst(1, "!fires help", 10.0));
    }

    [Fact]
    public void ExpiredEntries_AreForgotten()
    {
        var filter = new DuplicateFilter(2.0);
        filter.IsFirst(1, "a", 0.0);
        filter.IsFirst(2, "b", 1.0);

        filter.IsFirst(3, "c", 2.5);

        Assert.Equal(2, filter.Count); // "a" expired, "b" and "c" remain
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    public void Constructor_InvalidWindow_Throws(double window) =>
        Assert.Throws<ArgumentOutOfRangeException>("windowSeconds", () => new DuplicateFilter(window));
}
