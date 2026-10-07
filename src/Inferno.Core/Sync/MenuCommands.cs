using System;
using System.Collections.Generic;
using System.Globalization;
using Inferno.Core.Commands;
using Inferno.Core.Settings;

namespace Inferno.Core.Sync;

/// <summary>
/// Turns edits made in the client settings menu into ordinary <c>!fires</c> commands. The server runs them like
/// any other command, so permissions, validation and the audit log apply unchanged.
/// </summary>
public static class MenuCommands
{
    /// <summary>Commands that change an item from <paramref name="before"/> to <paramref name="after"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="prefabName"/> is blank or contains whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="before"/> or <paramref name="after"/> is null.</exception>
    public static IReadOnlyList<string> ForItem(string prefabName, ItemSettings before, ItemSettings after)
    {
        if (string.IsNullOrWhiteSpace(prefabName) || prefabName.IndexOfAny([' ', '\t', '\n', '\r']) >= 0)
        {
            throw new ArgumentException("Prefab name must be a single word.", nameof(prefabName));
        }

        if (before is null)
        {
            throw new ArgumentNullException(nameof(before));
        }

        if (after is null)
        {
            throw new ArgumentNullException(nameof(after));
        }

        // Always on last: setting a burn rate or schedule switches always on off, so the player's own always-on
        // choice in the same edit must be applied after them.
        var commands = new List<string>();
        if (before.BurnRateLevel != after.BurnRateLevel)
        {
            commands.Add(Command("burnrate", prefabName, after.BurnRateLevel.ToString(CultureInfo.InvariantCulture)));
        }

        if (before.Schedule != after.Schedule)
        {
            commands.Add(after.Schedule.IsAlwaysOn
                ? Command("schedule", prefabName, "off")
                : Command("schedule", prefabName, after.Schedule.OnTime.ToString(), after.Schedule.OffTime.ToString()));
        }

        if (before.Smoke != after.Smoke)
        {
            commands.Add(Command("smoke", prefabName, OnOff(after.Smoke)));
        }

        if (before.AlwaysOn != after.AlwaysOn)
        {
            commands.Add(Command("alwayson", prefabName, OnOff(after.AlwaysOn)));
        }

        return commands;
    }

    /// <summary>Commands that change the server-wide settings from <paramref name="before"/> to <paramref name="after"/>.</summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IReadOnlyList<string> ForGeneral(GeneralSettings before, GeneralSettings after)
    {
        if (before is null)
        {
            throw new ArgumentNullException(nameof(before));
        }

        if (after is null)
        {
            throw new ArgumentNullException(nameof(after));
        }

        var commands = new List<string>();
        AddIfChanged(commands, "adminonly", before.AdminOnly, after.AdminOnly);
        AddIfChanged(commands, "hidecommands", before.HideCommands, after.HideCommands);
        AddIfChanged(commands, "ignorerain", before.IgnoreRain, after.IgnoreRain);
        AddIfChanged(commands, "serverownership", before.ServerOwnership, after.ServerOwnership);
        return commands;
    }

    private static void AddIfChanged(List<string> commands, string name, bool before, bool after)
    {
        if (before != after)
        {
            commands.Add(Command(name, OnOff(after)));
        }
    }

    private static string Command(params string[] parts) => CommandParser.Prefix + " " + string.Join(" ", parts);

    private static string OnOff(bool value) => value ? "on" : "off";
}
