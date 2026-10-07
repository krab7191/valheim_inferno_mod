using System;
using System.Collections.Generic;
using System.Linq;
using Inferno.Core.Settings;
using Xunit;

namespace Inferno.Core.Tests.Settings;

public class SettingsDiffTests
{
    private static SettingsSnapshot Snapshot(GeneralSettings general, params (string Name, ItemSettings Settings)[] items)
    {
        var dict = new Dictionary<string, ItemSettings>();
        foreach (var (name, settings) in items)
        {
            dict[name] = settings;
        }

        return new SettingsSnapshot(general, dict);
    }

    [Fact]
    public void NoDifferences_ReturnsEmpty()
    {
        var a = Snapshot(GeneralSettings.Default, ("hearth", ItemSettings.AlwaysOnDefault));
        var b = Snapshot(GeneralSettings.Default, ("hearth", ItemSettings.AlwaysOnDefault));

        Assert.Empty(SettingsDiff.Compare(a, b));
    }

    [Fact]
    public void ReportsEveryChangedValue()
    {
        var before = Snapshot(
            GeneralSettings.Default,
            ("hearth", ItemSettings.AlwaysOnDefault),
            ("smelter", ItemSettings.Vanilla));
        var after = Snapshot(
            new GeneralSettings(adminOnly: true, hideCommands: false, ignoreRain: true, serverOwnership: true),
            ("hearth", ItemSettings.AlwaysOnDefault.WithBurnRate(3)),
            ("smelter", ItemSettings.Vanilla),
            ("new_item", ItemSettings.Vanilla)); // not in "before": not a change

        var changes = SettingsDiff.Compare(before, after);

        Assert.Equal(
            [
                "[General] AdminOnly: off -> on",
                "[General] HideCommands: on -> off",
                "[General] IgnoreRain: off -> on",
                "[General] ServerOwnership: off -> on",
                "[hearth] Settings: alwayson=on burnrate=0 schedule=always on smoke=on -> alwayson=on burnrate=3 schedule=always on smoke=on",
            ],
            changes.Select(c => c.ToString()));
    }

    [Fact]
    public void NullArguments_Throw()
    {
        var s = Snapshot(GeneralSettings.Default);

        Assert.Throws<ArgumentNullException>("before", () => SettingsDiff.Compare(null!, s));
        Assert.Throws<ArgumentNullException>("after", () => SettingsDiff.Compare(s, null!));
        Assert.Throws<ArgumentNullException>("general", () => new SettingsSnapshot(null!, new Dictionary<string, ItemSettings>()));
        Assert.Throws<ArgumentNullException>("items", () => new SettingsSnapshot(GeneralSettings.Default, null!));
    }
}
