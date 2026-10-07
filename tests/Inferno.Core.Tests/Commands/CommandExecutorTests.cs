using System;
using System.Collections.Generic;
using Inferno.Core.Catalog;
using Inferno.Core.Commands;
using Inferno.Core.Fuel;
using Inferno.Core.Settings;
using Inferno.Core.Time;
using Xunit;

namespace Inferno.Core.Tests.Commands;

public class CommandExecutorTests
{
    private static readonly CommandSender Player = new("Ragnar", "Steam_1", isAdmin: false);
    private static readonly CommandSender Admin = new("Lagertha", "Steam_2", isAdmin: true);

    private readonly FakeSettingsStore _store;
    private readonly CommandExecutor _executor;

    public CommandExecutorTests()
    {
        var catalog = TestData.Catalog();
        _store = new FakeSettingsStore(catalog);
        _executor = new CommandExecutor(catalog, _store);
    }

    private CommandResult Run(string text, CommandSender? sender = null)
    {
        Assert.Equal(ParseOutcome.Command, CommandParser.Parse(text, out var command, out _));
        return _executor.Execute(command, sender ?? Player);
    }

    // ---- Arguments ----

    [Fact]
    public void Constructor_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>("catalog", () => new CommandExecutor(null!, _store));
        Assert.Throws<ArgumentNullException>("store", () => new CommandExecutor(TestData.Catalog(), null!));
    }

    [Fact]
    public void Execute_NullArguments_Throw()
    {
        CommandParser.Parse("!fires help", out var command, out _);
        Assert.Throws<ArgumentNullException>("command", () => _executor.Execute(null!, Player));
        Assert.Throws<ArgumentNullException>("sender", () => _executor.Execute(command, null!));
    }

    [Fact]
    public void Describe_Null_Throws() =>
        Assert.Throws<ArgumentNullException>("item", () => _executor.Describe(null!));

    // ---- Read-only commands ----

    [Fact]
    public void Help_ListsCommands()
    {
        var result = Run("!fires help");

        Assert.Contains(result.Reply, l => l.StartsWith("schedule <item>", StringComparison.Ordinal));
        Assert.Empty(result.Changes);
        Assert.False(result.Denied);
    }

    [Fact]
    public void Status_DefaultsToRunning_AndUsesProvider()
    {
        Assert.Equal(["Inferno is running."], Run("!fires status").Reply);

        var executor = new CommandExecutor(TestData.Catalog(), _store, () => ["Inferno 1.2.3", "Tracking 5 fires"]);
        CommandParser.Parse("!fires status", out var command, out _);
        Assert.Equal(["Inferno 1.2.3", "Tracking 5 fires"], executor.Execute(command, Player).Reply);
    }

    [Fact]
    public void Status_AllowedEvenWhenAdminOnly()
    {
        _store.General = _store.General.WithAdminOnly(true);

        Assert.False(Run("!fires status").Denied);
    }

    [Fact]
    public void List_All_DescribesEveryItemWithDefaults()
    {
        var result = Run("!fires list");

        Assert.Equal(
            [
                "Hearth (hearth, light): alwayson=on burnrate=0 schedule=always on smoke=on",
                "Standing wood torch (piece_groundtorch_wood, light): alwayson=on burnrate=0 schedule=always on smoke=on",
                "Smelter (smelter, station): alwayson=off burnrate=0 schedule=n/a smoke=on",
            ],
            result.Reply);
    }

    [Fact]
    public void ReadOnlyCommands_AllowedEvenWhenAdminOnly()
    {
        _store.General = _store.General.WithAdminOnly(true);

        Assert.False(Run("!fires show hearth").Denied);
        Assert.False(Run("!fires list").Denied);
        Assert.False(Run("!fires help").Denied);
    }

    [Theory]
    [InlineData("!fires show nope", "Unknown item 'nope'. Use 'list' to see all items.")]
    [InlineData("!fires alwayson nope on", "Unknown item 'nope'. Use 'list' to see all items.")]
    [InlineData("!fires show standing wood torchx", "Unknown item 'standing wood torchx'. Did you mean: Standing wood torch?")]
    public void UnknownTarget_Explains(string text, string expected) =>
        Assert.Equal([expected], Run(text).Reply);

    [Fact]
    public void EmptyGroup_Explains()
    {
        var executor = new CommandExecutor(new([TestData.Torch]), _store);
        CommandParser.Parse("!fires show stations", out var command, out _);

        Assert.Equal(["No items in 'stations'."], executor.Execute(command, Player).Reply);
    }

    // ---- Item settings ----

    [Fact]
    public void AlwaysOn_Group_ChangesOnlyItemsThatDiffer()
    {
        var result = Run("!fires alwayson all on");

        Assert.Equal(["AlwaysOn = on: 1 of 3 item(s) changed."], result.Reply);
        var change = Assert.Single(result.Changes);
        Assert.Equal("smelter", change.Section);
        Assert.Equal("AlwaysOn", change.Setting);
        Assert.Equal("alwayson=off burnrate=0 schedule=always on smoke=on", change.OldValue);
        Assert.Equal("alwayson=on burnrate=0 schedule=always on smoke=on", change.NewValue);
        Assert.True(_store.GetItem("smelter").AlwaysOn);
        Assert.Equal(1, _store.Writes);
    }

    [Fact]
    public void InGameName_ChangesTheItem()
    {
        var result = Run("!fires burnrate standing wood torch -3");

        Assert.Equal(["BurnRate = -3: 1 of 1 item(s) changed."], result.Reply);
        Assert.Equal(-3, _store.GetItem("piece_groundtorch_wood").BurnRateLevel);
    }

    // ---- Word groups, presets, undo ----

    [Fact]
    public void WordGroup_SelectsMatchingItems_AndListsThem()
    {
        var catalog = new ItemCatalog([TestData.Torch, TestData.IronTorch, TestData.Hearth, TestData.Smelter]);
        var store = new FakeSettingsStore(catalog);
        var executor = new CommandExecutor(catalog, store);
        CommandParser.Parse("!fires burnrate torches -10", out var command, out _);

        var result = executor.Execute(command, Player);

        Assert.Equal(["BurnRate = -10: 2 of 2 item(s) changed.", "Changed: Standing iron torch, Standing wood torch"], result.Reply);
        Assert.Equal(0, store.GetItem("hearth").BurnRateLevel);
    }

    [Fact]
    public void FixedGroups_DoNotListNames() =>
        Assert.Equal(["AlwaysOn = off: 2 of 2 item(s) changed."], Run("!fires alwayson lights off").Reply);

    [Fact]
    public void ManyChanges_ListIsShortened()
    {
        var items = new List<CatalogItem>();
        for (var i = 0; i < 6; i++)
        {
            items.Add(new CatalogItem($"torch_{i}", $"Torch {i}", ItemKind.LightSource, new FuelItemInfo(4f, true)));
        }

        var catalog = new ItemCatalog(items);
        var executor = new CommandExecutor(catalog, new FakeSettingsStore(catalog));
        CommandParser.Parse("!fires alwayson torches off", out var command, out _);

        Assert.Equal("Changed: Torch 0, Torch 1, Torch 2, Torch 3, +2 more", executor.Execute(command, Player).Reply[1]);
    }

    [Fact]
    public void Schedule_NightWord()
    {
        Run("!fires schedule lights night");

        Assert.Equal("18:00-06:00", _store.GetItem("hearth").Schedule.ToString());
    }

    [Fact]
    public void Preset_Night_AppliesToLightsByDefault_KeepsSmoke()
    {
        Run("!fires smoke hearth off");

        var result = Run("!fires preset night");

        Assert.Equal("Preset = night: 2 of 2 item(s) changed.", result.Reply[0]);
        var hearth = _store.GetItem("hearth");
        Assert.False(hearth.AlwaysOn);
        Assert.Equal(-10, hearth.BurnRateLevel);
        Assert.Equal(DailySchedule.Night, hearth.Schedule);
        Assert.False(hearth.Smoke);
        Assert.Equal(ItemSettings.Vanilla, _store.GetItem("smelter"));
    }

    [Fact]
    public void Preset_Night_SkipsItemsWithoutSwitch()
    {
        var result = Run("!fires preset night all");

        Assert.Equal("Preset = night: 2 of 3 item(s) changed.", result.Reply[0]);
        Assert.Contains("1 item(s) skipped: they have no on/off switch and can't follow a schedule.", result.Reply);
    }

    [Fact]
    public void Preset_VanillaThenEternal()
    {
        Run("!fires preset vanilla");
        Assert.False(_store.GetItem("hearth").AlwaysOn);

        Run("!fires preset eternal hearth");
        Assert.Equal(ItemSettings.AlwaysOnDefault, _store.GetItem("hearth"));
        Assert.False(_store.GetItem("piece_groundtorch_wood").AlwaysOn);
    }

    [Fact]
    public void Preset_Unknown_ListsPresets() =>
        Assert.Equal(
            ["Unknown preset 'party'. Presets: eternal (always on, never needs fuel (default for lights)), night (lit 18:00-06:00, never uses fuel), vanilla (plain game behaviour)."],
            Run("!fires preset party").Reply);

    [Fact]
    public void Undo_RevertsOwnLastItemChange()
    {
        Run("!fires burnrate hearth 5");
        Run("!fires alwayson hearth off");

        var result = Run("!fires undo");

        Assert.Equal(["Undone: AlwaysOn = off on 'hearth'."], result.Reply);
        var change = Assert.Single(result.Changes);
        Assert.Equal("Undo", change.Setting);
        Assert.True(_store.GetItem("hearth").AlwaysOn);
        Assert.Equal(5, _store.GetItem("hearth").BurnRateLevel);
        Assert.Equal(["Nothing to undo."], Run("!fires undo").Reply);
    }

    [Fact]
    public void Undo_IsPerPlayer()
    {
        Run("!fires burnrate hearth 5", Player);

        Assert.Equal(["Nothing to undo."], Run("!fires undo", Admin).Reply);
        Assert.Equal(["Undone: BurnRate = 5 on 'hearth'."], Run("!fires undo", Player).Reply);
        Assert.Equal(0, _store.GetItem("hearth").BurnRateLevel);
    }

    [Fact]
    public void Undo_GeneralSetting_RestoresOnlyThatSetting()
    {
        Run("!fires ignorerain on", Player);
        Run("!fires hidecommands off", Admin);

        var result = Run("!fires undo", Player);

        Assert.Equal(["Undone: IgnoreRain = on."], result.Reply);
        Assert.Equal("[General] IgnoreRain: on -> off", Assert.Single(result.Changes).ToString());
        Assert.False(_store.General.IgnoreRain);
        Assert.False(_store.General.HideCommands);
    }

    [Fact]
    public void Undo_SkipsItemsAlreadyBack()
    {
        Run("!fires alwayson hearth off");
        Run("!fires alwayson hearth on", Admin);

        var result = Run("!fires undo");

        Assert.Empty(result.Changes);
        Assert.True(_store.GetItem("hearth").AlwaysOn);
    }

    [Fact]
    public void NoChange_DoesNotReplaceUndo()
    {
        Run("!fires burnrate hearth 5");
        Run("!fires alwayson hearth on"); // already on: nothing changed

        Assert.Equal(["Undone: BurnRate = 5 on 'hearth'."], Run("!fires undo").Reply);
    }

    [Fact]
    public void Undo_NeedsPermission()
    {
        Run("!fires burnrate hearth 5");
        _store.General = _store.General.WithAdminOnly(true);

        Assert.True(Run("!fires undo").Denied);
    }

    [Fact]
    public void BurnRate_SetsLevel()
    {
        var result = Run("!fires burnrate lights -5");

        Assert.Equal(["BurnRate = -5: 2 of 2 item(s) changed."], result.Reply);
        Assert.Equal(-5, _store.GetItem("hearth").BurnRateLevel);
        Assert.Equal(0, _store.GetItem("smelter").BurnRateLevel);
    }

    [Fact]
    public void Smoke_SetsFlag()
    {
        var result = Run("!fires smoke lights off");

        Assert.Equal(["Smoke = off: 2 of 2 item(s) changed."], result.Reply);
        Assert.False(_store.GetItem("hearth").Smoke);
        Assert.True(_store.GetItem("smelter").Smoke);
    }

    [Fact]
    public void Schedule_SkipsItemsWithoutSwitch()
    {
        var result = Run("!fires schedule all 18:00 06:00");

        Assert.Equal(
            ["Schedule = 18:00-06:00: 2 of 3 item(s) changed.", "1 item(s) skipped: they have no on/off switch and can't follow a schedule."],
            result.Reply);
        Assert.Equal("18:00-06:00", _store.GetItem("hearth").Schedule.ToString());
        Assert.True(_store.GetItem("smelter").Schedule.IsAlwaysOn);
    }

    [Fact]
    public void Reset_RestoresKindDefaults()
    {
        Run("!fires alwayson all off");
        Run("!fires burnrate all 7");

        var result = Run("!fires reset all");

        Assert.Equal(["Settings = defaults: 3 of 3 item(s) changed."], result.Reply);
        Assert.Equal(ItemSettings.AlwaysOnDefault, _store.GetItem("hearth"));
        Assert.Equal(ItemSettings.Vanilla, _store.GetItem("smelter"));
    }

    // ---- General settings ----

    [Theory]
    [InlineData("!fires hidecommands off", "HideCommands")]
    [InlineData("!fires ignorerain on", "IgnoreRain")]
    [InlineData("!fires adminonly on", "AdminOnly")]
    [InlineData("!fires serverownership on", "ServerOwnership")]
    public void GeneralSetting_Changes(string text, string setting)
    {
        var result = Run(text);

        var change = Assert.Single(result.Changes);
        Assert.Equal("General", change.Section);
        Assert.Equal(setting, change.Setting);
        Assert.NotEqual(change.OldValue, change.NewValue);
        Assert.Equal([$"{setting} = {change.NewValue}."], result.Reply);
    }

    [Fact]
    public void GeneralSetting_Unchanged_SaysSo()
    {
        var result = Run("!fires hidecommands on");

        Assert.Equal(["HideCommands is already on."], result.Reply);
        Assert.Empty(result.Changes);
    }

    [Fact]
    public void GeneralSettings_AreApplied()
    {
        Run("!fires ignorerain on");
        Run("!fires hidecommands off");
        Run("!fires serverownership on");
        Run("!fires adminonly on");

        Assert.True(_store.General.ServerOwnership);
        Assert.True(_store.General.IgnoreRain);
        Assert.False(_store.General.HideCommands);
        Assert.True(_store.General.AdminOnly);
    }

    // ---- Permissions ----

    [Fact]
    public void AdminOnly_AnyoneCanTurnOn_OnlyAdminsCanTurnOff()
    {
        Assert.False(Run("!fires adminonly on", Player).Denied);

        var denied = Run("!fires adminonly off", Player);
        Assert.True(denied.Denied);
        Assert.Equal(["Only admins can change Inferno settings while AdminOnly is on."], denied.Reply);
        Assert.Empty(denied.Changes);
        Assert.True(_store.General.AdminOnly);

        Assert.False(Run("!fires adminonly off", Admin).Denied);
        Assert.False(_store.General.AdminOnly);
    }

    [Fact]
    public void AdminOnly_BlocksAllChangesByNonAdmins()
    {
        _store.General = _store.General.WithAdminOnly(true);

        Assert.True(Run("!fires alwayson all off", Player).Denied);
        Assert.True(Run("!fires ignorerain on", Player).Denied);
        Assert.Equal(0, _store.Writes);
        Assert.False(Run("!fires alwayson all off", Admin).Denied);
    }

    // ---- Small types ----

    [Fact]
    public void CommandSender_ToString_ForLogs()
    {
        Assert.Equal("Ragnar (Steam_1)", Player.ToString());
        Assert.Equal("Lagertha (Steam_2, admin)", Admin.ToString());
        Assert.Throws<ArgumentNullException>("name", () => new CommandSender(null!, "x", false));
        Assert.Throws<ArgumentNullException>("platformId", () => new CommandSender("x", null!, false));
    }

    [Fact]
    public void SettingChange_ToString_ForLogs() =>
        Assert.Equal("[General] AdminOnly: off -> on", new SettingChange("General", "AdminOnly", "off", "on").ToString());
}
