using System;
using Inferno.Core.Commands;
using Xunit;

namespace Inferno.Core.Tests.Commands;

public class SignReplyTests
{
    [Fact]
    public void Summarize_TakesFirstLine() =>
        Assert.Equal("AlwaysOn = on: 1 of 1 item(s) changed.", SignReply.Summarize(["  AlwaysOn = on: 1 of 1 item(s) changed. ", "second"]));

    [Fact]
    public void Summarize_ShortensToSignLength()
    {
        var result = SignReply.Summarize([new string('x', 80)]);

        Assert.Equal(SignReply.MaxLength, result.Length);
        Assert.EndsWith("…", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Summarize_ExactLength_IsKept() =>
        Assert.Equal(new string('x', 50), SignReply.Summarize([new string('x', 50)]));

    [Fact]
    public void Summarize_NoReply_IsEmpty() => Assert.Equal(string.Empty, SignReply.Summarize([]));

    [Fact]
    public void Summarize_Null_Throws() =>
        Assert.Throws<ArgumentNullException>("reply", () => SignReply.Summarize(null!));
}
