using Inferno.Core.Commands;
using Xunit;

namespace Inferno.Core.Tests.Commands;

public class CommandParserTests
{
    private static ParsedCommand Ok(string text)
    {
        Assert.Equal(ParseOutcome.Command, CommandParser.Parse(text, out var command, out var error));
        Assert.Equal(string.Empty, error);
        return command;
    }

    private static string Invalid(string text)
    {
        Assert.Equal(ParseOutcome.Invalid, CommandParser.Parse(text, out var command, out var error));
        Assert.Null(command);
        return error;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello there")]
    [InlineData("!firesx help")]
    [InlineData("fires help")]
    [InlineData("/fires help")]
    [InlineData("listkeys")]
    [InlineData("listkeys all")]
    [InlineData("listkeys !fires help")]
    [InlineData("fires listkeys")]
    public void Parse_OrdinaryChat_IsNotACommand(string? text)
    {
        Assert.Equal(ParseOutcome.NotACommand, CommandParser.Parse(text, out var command, out var error));
        Assert.Null(command);
        Assert.Equal(string.Empty, error);
    }

    [Theory]
    [InlineData("!fires")]
    [InlineData("!FIRES")]
    [InlineData("  !fires   help  ")]
    [InlineData("!fires\thelp")]
    [InlineData("listkeys fires")]
    [InlineData("ListKeys FIRES help")]
    public void Parse_Help(string text) => Assert.Equal(CommandKind.Help, Ok(text).Kind);

    [Fact]
    public void Parse_Status() => Assert.Equal(CommandKind.Status, Ok("!fires status").Kind);

    [Fact]
    public void Parse_TooLong_IsInvalid()
    {
        var text = "!fires show " + new string('x', CommandParser.MaxLength);

        Assert.Equal("Command too long (max 200 characters).", Invalid(text));
    }

    [Fact]
    public void Parse_LongOrdinaryChat_IsNotACommand() =>
        Assert.Equal(ParseOutcome.NotACommand, CommandParser.Parse(new string('x', 500), out _, out _));

    [Fact]
    public void Parse_ConsoleForm_MatchesChatForm()
    {
        var chat = Ok("!fires schedule lights 18:00 06:00");
        var console = Ok("listkeys fires schedule lights 18:00 06:00");

        Assert.Equal(chat.Kind, console.Kind);
        Assert.Equal(chat.Target, console.Target);
        Assert.Equal(chat.Schedule, console.Schedule);
    }

    [Theory]
    [InlineData("!fires burnrate", "Usage: !fires burnrate")]
    [InlineData("listkeys fires burnrate", "Usage: listkeys fires burnrate")]
    [InlineData("listkeys fires nope", "Unknown command 'nope'. Type listkeys fires help")]
    public void Parse_Invalid_UsesTheSameFormTheUserTyped(string text, string expectedStart) =>
        Assert.StartsWith(expectedStart, Invalid(text));

    [Theory]
    [InlineData("!fires list", "all")]
    [InlineData("!fires list lights", "lights")]
    public void Parse_List(string text, string target)
    {
        var command = Ok(text);
        Assert.Equal(CommandKind.List, command.Kind);
        Assert.Equal(target, command.Target);
    }

    [Theory]
    [InlineData("!fires show hearth", CommandKind.Show)]
    [InlineData("!fires reset hearth", CommandKind.Reset)]
    public void Parse_TargetOnlyCommands(string text, CommandKind kind)
    {
        var command = Ok(text);
        Assert.Equal(kind, command.Kind);
        Assert.Equal("hearth", command.Target);
    }

    [Theory]
    [InlineData("!fires alwayson lights on", true)]
    [InlineData("!fires AlwaysOn lights OFF", false)]
    public void Parse_AlwaysOn(string text, bool expected)
    {
        var command = Ok(text);
        Assert.Equal(CommandKind.AlwaysOn, command.Kind);
        Assert.Equal("lights", command.Target);
        Assert.Equal(expected, command.Flag);
    }

    [Theory]
    [InlineData("!fires smoke lights off", false)]
    [InlineData("!fires smoke hearth ON", true)]
    public void Parse_Smoke(string text, bool expected)
    {
        var command = Ok(text);
        Assert.Equal(CommandKind.Smoke, command.Kind);
        Assert.Equal(expected, command.Flag);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("-10", -10)]
    [InlineData("+10", 10)]
    [InlineData("5", 5)]
    [InlineData("-3", -3)]
    public void Parse_BurnRate(string value, int expected)
    {
        var command = Ok($"!fires burnrate hearth {value}");
        Assert.Equal(CommandKind.BurnRate, command.Kind);
        Assert.Equal(expected, command.Number);
    }

    [Theory]
    [InlineData("11")]
    [InlineData("-11")]
    [InlineData("100")]
    [InlineData("-")]
    [InlineData("")]
    [InlineData("1.5")]
    [InlineData("ten")]
    public void Parse_BurnRate_Invalid(string value) =>
        Assert.StartsWith("Usage: !fires burnrate", Invalid($"!fires burnrate hearth {value}"));

    [Fact]
    public void Parse_Schedule_Window()
    {
        var command = Ok("!fires schedule lights 18:00 6:00");
        Assert.Equal(CommandKind.Schedule, command.Kind);
        Assert.Equal("lights", command.Target);
        Assert.Equal("18:00-06:00", command.Schedule.ToString());
    }

    [Theory]
    [InlineData("!fires schedule lights off")]
    [InlineData("!fires schedule lights 00:00 24:00")]
    public void Parse_Schedule_Off(string text) => Assert.True(Ok(text).Schedule.IsAlwaysOn);

    [Theory]
    [InlineData("!fires schedule lights")]
    [InlineData("!fires schedule lights on")]
    [InlineData("!fires schedule lights 18:00")]
    [InlineData("!fires schedule lights 25:00 06:00")]
    [InlineData("!fires schedule lights 18:00 6pm")]
    public void Parse_Schedule_Invalid(string text) => Assert.StartsWith("Usage: !fires schedule", Invalid(text));

    [Theory]
    [InlineData("!fires adminonly on", CommandKind.AdminOnly, true)]
    [InlineData("!fires adminonly off", CommandKind.AdminOnly, false)]
    [InlineData("!fires hidecommands off", CommandKind.HideCommands, false)]
    [InlineData("!fires ignorerain on", CommandKind.IgnoreRain, true)]
    [InlineData("!fires serverownership on", CommandKind.ServerOwnership, true)]
    public void Parse_GeneralSettings(string text, CommandKind kind, bool expected)
    {
        var command = Ok(text);
        Assert.Equal(kind, command.Kind);
        Assert.Equal(expected, command.Flag);
        Assert.Null(command.Target);
    }

    [Theory]
    [InlineData("!fires help me", "Usage: !fires help")]
    [InlineData("!fires status now", "Usage: !fires status")]
    [InlineData("!fires list a b", "Usage: !fires list")]
    [InlineData("!fires show", "Usage: !fires show")]
    [InlineData("!fires reset", "Usage: !fires reset")]
    [InlineData("!fires alwayson lights maybe", "Usage: !fires alwayson")]
    [InlineData("!fires alwayson on", "Usage: !fires alwayson")]
    [InlineData("!fires adminonly", "Usage: !fires adminonly")]
    [InlineData("!fires hidecommands yes", "Usage: !fires hidecommands")]
    [InlineData("!fires ignorerain", "Usage: !fires ignorerain")]
    [InlineData("!fires serverownership", "Usage: !fires serverownership")]
    [InlineData("!fires burnrate", "Usage: !fires burnrate")]
    [InlineData("!fires smoke lights", "Usage: !fires smoke")]
    [InlineData("!fires explode", "Unknown command 'explode'. Type !fires help")]
    public void Parse_Invalid_GivesUsage(string text, string expectedStart) =>
        Assert.StartsWith(expectedStart, Invalid(text));
}
