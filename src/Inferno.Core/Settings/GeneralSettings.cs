namespace Inferno.Core.Settings;

/// <summary>Server-wide settings (config section <c>[General]</c>). Immutable.</summary>
public sealed class GeneralSettings
{
    /// <summary>Creates general settings.</summary>
    public GeneralSettings(bool adminOnly, bool hideCommands, bool ignoreRain, bool serverOwnership = false)
    {
        AdminOnly = adminOnly;
        HideCommands = hideCommands;
        IgnoreRain = ignoreRain;
        ServerOwnership = serverOwnership;
    }

    /// <summary>
    /// Defaults: anyone may change settings, commands are hidden, lights go out in the rain, fires are owned by
    /// players as in vanilla.
    /// </summary>
    public static GeneralSettings Default { get; } = new(adminOnly: false, hideCommands: true, ignoreRain: false, serverOwnership: false);

    /// <summary>Only admins may change settings.</summary>
    public bool AdminOnly { get; }

    /// <summary>Hide <c>!fires</c> commands from other players' chat.</summary>
    public bool HideCommands { get; }

    /// <summary>Switch lights back on after rain or wind put them out.</summary>
    public bool IgnoreRain { get; }

    /// <summary>Experimental: the server keeps ownership of fires (see <c>RTM.md</c> §4.13).</summary>
    public bool ServerOwnership { get; }

    /// <summary>Returns a copy with <see cref="AdminOnly"/> changed.</summary>
    public GeneralSettings WithAdminOnly(bool value) => new(value, HideCommands, IgnoreRain, ServerOwnership);

    /// <summary>Returns a copy with <see cref="HideCommands"/> changed.</summary>
    public GeneralSettings WithHideCommands(bool value) => new(AdminOnly, value, IgnoreRain, ServerOwnership);

    /// <summary>Returns a copy with <see cref="IgnoreRain"/> changed.</summary>
    public GeneralSettings WithIgnoreRain(bool value) => new(AdminOnly, HideCommands, value, ServerOwnership);

    /// <summary>Returns a copy with <see cref="ServerOwnership"/> changed.</summary>
    public GeneralSettings WithServerOwnership(bool value) => new(AdminOnly, HideCommands, IgnoreRain, value);
}
