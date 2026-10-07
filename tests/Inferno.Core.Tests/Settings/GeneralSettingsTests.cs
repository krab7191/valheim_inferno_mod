using Inferno.Core.Settings;
using Xunit;

namespace Inferno.Core.Tests.Settings;

public class GeneralSettingsTests
{
    [Fact]
    public void Default_MatchesAgreedDefaults()
    {
        var settings = GeneralSettings.Default;

        Assert.False(settings.AdminOnly);
        Assert.True(settings.HideCommands);
        Assert.False(settings.IgnoreRain);
        Assert.False(settings.ServerOwnership);
    }

    [Fact]
    public void With_ChangesOnlyOneValue()
    {
        var start = GeneralSettings.Default;

        var adminOnly = start.WithAdminOnly(true);
        Assert.True(adminOnly.AdminOnly);
        Assert.True(adminOnly.HideCommands);
        Assert.False(adminOnly.IgnoreRain);

        var hide = start.WithHideCommands(false);
        Assert.False(hide.HideCommands);
        Assert.False(hide.AdminOnly);
        Assert.False(hide.IgnoreRain);

        var rain = start.WithIgnoreRain(true);
        Assert.True(rain.IgnoreRain);
        Assert.False(rain.AdminOnly);
        Assert.True(rain.HideCommands);
        Assert.False(rain.ServerOwnership);

        var owned = start.WithServerOwnership(true).WithAdminOnly(true).WithHideCommands(false).WithIgnoreRain(true);
        Assert.True(owned.ServerOwnership);
        Assert.True(owned.AdminOnly);
        Assert.False(owned.HideCommands);
        Assert.True(owned.IgnoreRain);
    }
}
