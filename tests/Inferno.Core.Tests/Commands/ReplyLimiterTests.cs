using System;
using Inferno.Core.Commands;
using Xunit;

namespace Inferno.Core.Tests.Commands;

public class ReplyLimiterTests
{
    [Fact]
    public void ShortReply_IsUnchanged()
    {
        string[] lines = ["a", "b", "c"];

        Assert.Same(lines, ReplyLimiter.Limit(lines, 3));
    }

    [Fact]
    public void LongReply_IsCutWithPointerToConsole()
    {
        var result = ReplyLimiter.Limit(["a", "b", "c", "d", "e"], 3);

        Assert.Equal(3, result.Count);
        Assert.Equal("a", result[0]);
        Assert.Equal("b", result[1]);
        Assert.Equal("… 3 more line(s). Use the F5 console ('listkeys fires …') to see everything.", result[2]);
    }

    [Fact]
    public void InvalidArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>("lines", () => ReplyLimiter.Limit(null!, 3));
        Assert.Throws<ArgumentOutOfRangeException>("maxLines", () => ReplyLimiter.Limit(["a"], 1));
    }
}
