using System;
using System.Collections.Generic;
using Inferno.Core.Commands;
using Inferno.Core.Settings;
using Inferno.Core.Sync;
using Inferno.Core.Time;
using Xunit;

namespace Inferno.Core.Tests.Sync;

public class SettingsMessageTests
{
    private static readonly DailySchedule Night = new(TimeOfDay.FromHourMinute(18, 30), TimeOfDay.FromHourMinute(6, 0));

    private static SettingsMessage Sample() => new(
        canEdit: true,
        new GeneralSettings(adminOnly: true, hideCommands: false, ignoreRain: true, serverOwnership: false),
        new Dictionary<string, ItemSettings>
        {
            ["hearth"] = ItemSettings.AlwaysOnDefault,
            ["piece_groundtorch_wood"] = new ItemSettings(false, -10, Night, smoke: false),
        });

    [Fact]
    public void RoundTrip_PreservesEverything()
    {
        var original = Sample();

        Assert.True(SettingsMessage.TryDecode(original.Encode(), out var decoded));

        Assert.True(decoded.CanEdit);
        Assert.True(decoded.General.AdminOnly);
        Assert.False(decoded.General.HideCommands);
        Assert.True(decoded.General.IgnoreRain);
        Assert.False(decoded.General.ServerOwnership);
        Assert.Equal(2, decoded.Items.Count);
        Assert.Equal(ItemSettings.AlwaysOnDefault, decoded.Items["HEARTH"]);
        Assert.Equal(original.Items["piece_groundtorch_wood"], decoded.Items["piece_groundtorch_wood"]);
    }

    [Fact]
    public void Encode_IsStableText()
    {
        var text = new SettingsMessage(false, GeneralSettings.Default, new Dictionary<string, ItemSettings> { ["x"] = ItemSettings.Vanilla }).Encode();

        Assert.Equal("inferno-settings\t1\ncanedit\t0\ngeneral\t0\t1\t0\t0\nitem\tx\t0\t0\t0\t0\t1\n", text);
    }

    [Fact]
    public void Encode_NameWithTab_Throws()
    {
        var message = new SettingsMessage(true, GeneralSettings.Default, new Dictionary<string, ItemSettings> { ["a\tb"] = ItemSettings.Vanilla });
        Assert.Throws<ArgumentException>(() => message.Encode());

        var newline = new SettingsMessage(true, GeneralSettings.Default, new Dictionary<string, ItemSettings> { ["a\nb"] = ItemSettings.Vanilla });
        Assert.Throws<ArgumentException>(() => newline.Encode());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("inferno-settings\t1\ncanedit\t0\n")]                                           // too short
    [InlineData("hello\t1\ncanedit\t0\ngeneral\t0\t1\t0\t0\n")]                                 // wrong header
    [InlineData("inferno-settings\t2\ncanedit\t0\ngeneral\t0\t1\t0\t0\n")]                      // other version
    [InlineData("inferno-settings\tx\ncanedit\t0\ngeneral\t0\t1\t0\t0\n")]                      // bad version
    [InlineData("inferno-settings\t1\ncanedit\t2\ngeneral\t0\t1\t0\t0\n")]                      // bad bit
    [InlineData("inferno-settings\t1\ncanedit\t0\ngeneral\t0\t1\t0\n")]                         // too few fields
    [InlineData("inferno-settings\t1\ncanedit\t0\ngeneral\t0\t1\t0\tx\n")]
    [InlineData("inferno-settings\t1\ncanedit\t0\ngeneral\tx\t1\t0\t0\n")]
    [InlineData("inferno-settings\t1\ncanedit\t0\ngeneral\t0\tx\t0\t0\n")]
    [InlineData("inferno-settings\t1\ncanedit\t0\ngeneral\t0\t1\tx\t0\n")]
    [InlineData("inferno-settings\t1\nxxx\t0\ngeneral\t0\t1\t0\t0\n")]
    [InlineData("inferno-settings\t1\ncanedit\t0\nxxx\t0\t1\t0\t0\n")]
    [InlineData("inferno-settings\t1\ncanedit\t0\ngeneral\t0\t1\t0\t0\nitem\t\t0\t0\t0\t0\t1\n")]   // empty name
    [InlineData("inferno-settings\t1\ncanedit\t0\ngeneral\t0\t1\t0\t0\nitem\tx\t2\t0\t0\t0\t1\n")]  // bad bit
    [InlineData("inferno-settings\t1\ncanedit\t0\ngeneral\t0\t1\t0\t0\nitem\tx\t0\t11\t0\t0\t1\n")] // burn rate too high
    [InlineData("inferno-settings\t1\ncanedit\t0\ngeneral\t0\t1\t0\t0\nitem\tx\t0\t-11\t0\t0\t1\n")]
    [InlineData("inferno-settings\t1\ncanedit\t0\ngeneral\t0\t1\t0\t0\nitem\tx\t0\tq\t0\t0\t1\n")]
    [InlineData("inferno-settings\t1\ncanedit\t0\ngeneral\t0\t1\t0\t0\nitem\tx\t0\t0\t1440\t0\t1\n")] // minutes out of range
    [InlineData("inferno-settings\t1\ncanedit\t0\ngeneral\t0\t1\t0\t0\nitem\tx\t0\t0\t-1\t0\t1\n")]
    [InlineData("inferno-settings\t1\ncanedit\t0\ngeneral\t0\t1\t0\t0\nitem\tx\t0\t0\t0\tq\t1\n")]
    [InlineData("inferno-settings\t1\ncanedit\t0\ngeneral\t0\t1\t0\t0\nitem\tx\t0\t0\t0\t0\t9\n")]
    [InlineData("inferno-settings\t1\ncanedit\t0\ngeneral\t0\t1\t0\t0\nitem\tx\t0\t0\t0\t0\n")]       // too few fields
    [InlineData("inferno-settings\t1\ncanedit\t0\ngeneral\t0\t1\t0\t0\nitem\tx\t0\t0\t0\t0\t1\nitem\tX\t0\t0\t0\t0\t1\n")] // duplicate
    public void TryDecode_Malformed_ReturnsFalse(string? text)
    {
        Assert.False(SettingsMessage.TryDecode(text, out var message));
        Assert.Null(message);
    }

    [Fact]
    public void TryDecode_NoItems_IsValid()
    {
        Assert.True(SettingsMessage.TryDecode("inferno-settings\t1\ncanedit\t1\ngeneral\t0\t1\t0\t1\n", out var message));
        Assert.Empty(message.Items);
        Assert.True(message.General.ServerOwnership);
    }

    [Fact]
    public void Constructor_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>("general", () => new SettingsMessage(true, null!, new Dictionary<string, ItemSettings>()));
        Assert.Throws<ArgumentNullException>("items", () => new SettingsMessage(true, GeneralSettings.Default, null!));
    }
}

public class MenuCommandsTests
{
    private static readonly DailySchedule Night = new(TimeOfDay.FromHourMinute(18, 0), TimeOfDay.FromHourMinute(6, 0));

    [Fact]
    public void ForItem_NoChange_NoCommands() =>
        Assert.Empty(MenuCommands.ForItem("hearth", ItemSettings.Vanilla, ItemSettings.Vanilla));

    [Fact]
    public void ForItem_EachChange_OneCommand()
    {
        var after = new ItemSettings(true, -4, Night, smoke: false);

        Assert.Equal(
            [
                "!fires alwayson hearth on",
                "!fires burnrate hearth -4",
                "!fires schedule hearth 18:00 06:00",
                "!fires smoke hearth off",
            ],
            MenuCommands.ForItem("hearth", ItemSettings.Vanilla, after));
    }

    [Fact]
    public void ForItem_ScheduleRemoved_SaysOff() =>
        Assert.Equal(
            ["!fires schedule hearth off"],
            MenuCommands.ForItem("hearth", ItemSettings.Vanilla.WithSchedule(Night), ItemSettings.Vanilla));

    [Fact]
    public void ForItem_CommandsParseBackToTheSameChange()
    {
        var after = new ItemSettings(false, 7, Night, smoke: false);

        foreach (var text in MenuCommands.ForItem("hearth", ItemSettings.AlwaysOnDefault, after))
        {
            Assert.Equal(ParseOutcome.Command, CommandParser.Parse(text, out var command, out _));
            Assert.Equal("hearth", command.Target);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("two words")]
    [InlineData("tab\tname")]
    [InlineData(null)]
    public void ForItem_BadName_Throws(string? name) =>
        Assert.Throws<ArgumentException>("prefabName", () => MenuCommands.ForItem(name!, ItemSettings.Vanilla, ItemSettings.Vanilla));

    [Fact]
    public void ForItem_NullSettings_Throw()
    {
        Assert.Throws<ArgumentNullException>("before", () => MenuCommands.ForItem("x", null!, ItemSettings.Vanilla));
        Assert.Throws<ArgumentNullException>("after", () => MenuCommands.ForItem("x", ItemSettings.Vanilla, null!));
    }

    [Fact]
    public void ForGeneral_EachChange_OneCommand()
    {
        var after = new GeneralSettings(adminOnly: true, hideCommands: false, ignoreRain: true, serverOwnership: true);

        Assert.Equal(
            ["!fires adminonly on", "!fires hidecommands off", "!fires ignorerain on", "!fires serverownership on"],
            MenuCommands.ForGeneral(GeneralSettings.Default, after));
        Assert.Empty(MenuCommands.ForGeneral(after, after));
    }

    [Fact]
    public void ForGeneral_NullSettings_Throw()
    {
        Assert.Throws<ArgumentNullException>("before", () => MenuCommands.ForGeneral(null!, GeneralSettings.Default));
        Assert.Throws<ArgumentNullException>("after", () => MenuCommands.ForGeneral(GeneralSettings.Default, null!));
    }
}
