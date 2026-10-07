using Inferno.Core.Commands;
using Xunit;

namespace Inferno.Core.Tests.Commands;

public class RichTextTests
{
    [Theory]
    [InlineData("<color=green>!fires status", "!fires status")]
    [InlineData("<color=#FF0000><b>!fires show hot tub</b></color>", "!fires show hot tub")]
    [InlineData("<size=40>!fires</size> help", "!fires help")]
    [InlineData("plain", "plain")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Strip_RemovesTags(string? text, string expected) => Assert.Equal(expected, RichText.Strip(text));

    [Theory]
    [InlineData("<color=green>!fires status", "<color=green>")]
    [InlineData("  <color=green> <b>!fires status", "<color=green> <b>")]
    [InlineData("!fires status", "")]
    [InlineData("</color>!fires status", "")]
    [InlineData(null, "")]
    public void Leading_ReturnsOpeningTags(string? text, string expected) => Assert.Equal(expected, RichText.Leading(text));

    [Theory]
    [InlineData("<color=green>!fires status")]
    [InlineData("<color=#00ff00><b>!fires status</b></color>")]
    [InlineData("<size=30>listkeys fires status")]
    public void Parser_IgnoresFormatting(string text) =>
        Assert.Equal(ParseOutcome.Command, CommandParser.Parse(text, out _, out _));

    [Fact]
    public void Parser_FormattedNamesStillResolve()
    {
        Assert.Equal(ParseOutcome.Command, CommandParser.Parse("<color=red>!fires show <b>hot tub</b>", out var command, out _));
        Assert.Equal("hot tub", command.Target);
    }
}
