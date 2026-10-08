using System;
using Inferno.Core.Areas;
using Inferno.Core.Commands;
using Inferno.Core.Settings;
using Inferno.Core.Time;
using Xunit;

namespace Inferno.Core.Tests.Commands;

public class NearbyCommandTests
{
    private static readonly CommandSender Player = new("Ragnar", "Steam_1", isAdmin: false);

    // Around the sign: two hearths (one in a ward the player can't use), a torch and a smelter.
    private static readonly NearbySelection Selection = new(
        [
            new NearbyObject("1:1", "hearth", allowed: true),
            new NearbyObject("1:2", "hearth", allowed: false),
            new NearbyObject("1:3", "piece_groundtorch_wood", allowed: true),
            new NearbyObject("1:4", "smelter", allowed: true),
            new NearbyObject("1:5", "modded_unknown", allowed: true), // not in the catalog: ignored
        ],
        "in this ward's area");

    private readonly FakeSettingsStore _store;
    private readonly CommandExecutor _executor;

    public NearbyCommandTests()
    {
        var catalog = TestData.Catalog();
        _store = new FakeSettingsStore(catalog);
        _executor = new CommandExecutor(catalog, _store);
    }

    private float? _lastRadius = -1f;

    private CommandResult Run(string text, Func<float?, NearbySelection>? nearby = null)
    {
        Assert.Equal(ParseOutcome.Command, CommandParser.Parse(text, out var command, out _));
        return _executor.Execute(command, Player, nearby ?? (radius =>
        {
            _lastRadius = radius;
            return Selection;
        }));
    }

    [Fact]
    public void Preset_Nearby_ChangesOnlyAllowedFiresThere_NotTheItemType()
    {
        var result = Run("!fires preset night nearby");

        Assert.Equal(
            [
                "Preset = night: 2 of 4 fire(s) in this ward's area changed.",
                "1 skipped: they're in a ward you have no access to.",
                "1 skipped: no on/off switch, so no schedule.",
            ],
            result.Reply);
        Assert.Equal(DailySchedule.Night, _store.GetObject("1:1")!.Schedule);
        Assert.Null(_store.GetObject("1:2"));
        Assert.Equal(DailySchedule.Night, _store.GetObject("1:3")!.Schedule);
        Assert.Equal(ItemSettings.AlwaysOnDefault, _store.GetItem("hearth")); // item type untouched
        Assert.Equal(2, result.Changes.Count);
        Assert.Equal("hearth 1:1", result.Changes[0].Section);
        Assert.Equal("follows item type", result.Changes[0].OldValue);
    }

    [Fact]
    public void ItemWord_Nearby_FiltersByItem()
    {
        var result = Run("!fires burnrate standing wood torch nearby -5");

        Assert.Equal(
            ["BurnRate = -5: 1 of 1 fire(s) in this ward's area changed.", "AlwaysOn turned off for 1 of them so this takes effect."],
            result.Reply);
        Assert.Equal(-5, _store.GetObject("1:3")!.BurnRateLevel);
        Assert.Null(_store.GetObject("1:1"));
    }

    [Fact]
    public void NoChange_IsNotStored()
    {
        var result = Run("!fires alwayson hearth nearby on"); // already on via its item type

        Assert.Equal("AlwaysOn = on: 0 of 2 fire(s) in this ward's area changed.", result.Reply[0]);
        Assert.Null(_store.GetObject("1:1"));
        Assert.Equal(["Nothing to undo."], Run("!fires undo").Reply);
    }

    [Fact]
    public void Reset_Nearby_ClearsOwnSettings()
    {
        Run("!fires alwayson nearby off");

        var result = Run("!fires reset nearby");

        Assert.Equal(["2 of 4 fire(s) in this ward's area back to their item type's settings.", "1 skipped: they're in a ward you have no access to."], result.Reply);
        Assert.Null(_store.GetObject("1:1"));
        Assert.Null(_store.GetObject("1:4"));
        Assert.Equal("follows item type", result.Changes[0].NewValue);
    }

    [Fact]
    public void Undo_RestoresObjects()
    {
        Run("!fires alwayson hearth nearby off");
        Run("!fires burnrate hearth nearby 3");

        Assert.Equal(["Undone: BurnRate = 3 on 'hearth nearby'. (1 more to undo)"], Run("!fires undo").Reply);
        Assert.Equal(0, _store.GetObject("1:1")!.BurnRateLevel);
        Assert.False(_store.GetObject("1:1")!.AlwaysOn);

        var last = Run("!fires undo");
        Assert.Equal(["Undone: AlwaysOn = off on 'hearth nearby'."], last.Reply);
        Assert.Null(_store.GetObject("1:1"));
        Assert.Equal("follows item type", Assert.Single(last.Changes).NewValue);
    }

    [Fact]
    public void Undo_ObjectAlreadyBack_NoChange()
    {
        Run("!fires alwayson hearth nearby off");
        _store.SetObject("1:1", null); // reset by someone else meanwhile

        var result = Run("!fires undo");

        Assert.Equal(["Undone: AlwaysOn = off on 'hearth nearby'."], result.Reply);
        Assert.Empty(result.Changes);
    }

    [Fact]
    public void Show_Nearby_GroupsByItemAndMarksOwnSettings()
    {
        Run("!fires alwayson standing wood torch nearby off");

        var result = Run("!fires show nearby");

        Assert.Equal(
            [
                "4 fire(s) in this ward's area:",
                "Hearth x2: alwayson=on burnrate=0 schedule=always on smoke=on",
                "Smelter x1: alwayson=off burnrate=0 schedule=always on smoke=on",
                "Standing wood torch x1 (own settings): alwayson=off burnrate=0 schedule=always on smoke=on",
            ],
            result.Reply);
        Assert.Equal(result.Reply, Run("!fires list nearby").Reply);
    }

    [Theory]
    [InlineData("!fires show hot tub nearby", "Unknown item 'hot tub'. Use 'list' to see all items.")]
    [InlineData("!fires alwayson hearth nearby on", "No 'hearth' in this ward's area.")]
    [InlineData("!fires preset night nearby", "No fires in this ward's area.")]
    public void Nearby_Errors(string text, string expected)
    {
        var empty = new NearbySelection([], "in this ward's area");

        Assert.Equal([expected], Run(text, _ => text.Contains("hot tub", StringComparison.Ordinal) ? Selection : empty).Reply);
    }

    // ---- Radius ----

    [Theory]
    [InlineData("!fires schedule nearby 2 13:00 14:00", 2f)]
    [InlineData("!fires alwayson standing wood torch nearby 2.5 off", 2.5f)]
    [InlineData("!fires burnrate torches nearby 40 -5", 40f)]
    [InlineData("!fires show nearby 10", 10f)]
    [InlineData("!fires reset nearby 1", 1f)]
    [InlineData("!fires preset night nearby 100", 100f)]
    [InlineData("!fires preset night nearby", null)]
    public void Radius_IsPassedToTheProvider(string text, float? expected)
    {
        Run(text);

        Assert.Equal(expected, _lastRadius);
    }

    [Theory]
    [InlineData("!fires schedule nearby 0 13:00 14:00")]
    [InlineData("!fires show nearby 101")]
    [InlineData("!fires show nearby -3")]
    [InlineData("!fires show nearby NaN")]
    public void Radius_OutOfRange_Explains(string text)
    {
        var result = Run(text);

        Assert.Equal(["Radius must be 1 to 100 m."], result.Reply);
        Assert.Equal(-1f, _lastRadius); // provider never asked
    }

    [Fact]
    public void Radius_ShowsInUndo()
    {
        Run("!fires alwayson nearby 3 off");

        Assert.Equal("Undone: AlwaysOn = off on 'nearby 3'.", Run("!fires undo").Reply[0]);
    }

    [Theory]
    [InlineData("!fires show hot tub nearby x 5")]  // "nearby" not near the end: just an item name
    [InlineData("!fires show nearby x")]           // word after nearby isn't a number
    public void NotNearbyForm_IsTreatedAsItemName(string text) =>
        Assert.StartsWith("Unknown item", Run(text).Reply[0], StringComparison.Ordinal);

    [Fact]
    public void Nearby_WithoutLocation_Explains()
    {
        CommandParser.Parse("!fires preset night nearby", out var command, out _);

        Assert.Equal(["'nearby' only works on a sign, in chat or in the F5 console."], _executor.Execute(command, Player).Reply);
    }

    [Fact]
    public void Nearby_RespectsAdminOnly()
    {
        _store.General = _store.General.WithAdminOnly(true);

        Assert.True(Run("!fires preset night nearby").Denied);
        Assert.False(Run("!fires show nearby").Denied);
    }
}
