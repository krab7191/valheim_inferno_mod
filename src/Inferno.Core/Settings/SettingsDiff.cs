using System;
using System.Collections.Generic;
using Inferno.Core.Commands;

namespace Inferno.Core.Settings;

/// <summary>
/// Compares two snapshots of all settings. Used to log what changed when the config file is edited by hand.
/// </summary>
public static class SettingsDiff
{
    /// <summary>Returns every difference between two snapshots, general settings first.</summary>
    /// <param name="before">Settings before the change.</param>
    /// <param name="after">Settings after the change.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IReadOnlyList<SettingChange> Compare(SettingsSnapshot before, SettingsSnapshot after)
    {
        if (before is null)
        {
            throw new ArgumentNullException(nameof(before));
        }

        if (after is null)
        {
            throw new ArgumentNullException(nameof(after));
        }

        var changes = new List<SettingChange>();
        AddIfChanged(changes, "AdminOnly", before.General.AdminOnly, after.General.AdminOnly);
        AddIfChanged(changes, "HideCommands", before.General.HideCommands, after.General.HideCommands);
        AddIfChanged(changes, "IgnoreRain", before.General.IgnoreRain, after.General.IgnoreRain);
        AddIfChanged(changes, "ServerOwnership", before.General.ServerOwnership, after.General.ServerOwnership);

        foreach (var entry in after.Items)
        {
            if (before.Items.TryGetValue(entry.Key, out var old) && !old.Equals(entry.Value))
            {
                changes.Add(new SettingChange(entry.Key, "Settings", old.ToString(), entry.Value.ToString()));
            }
        }

        return changes;
    }

    private static void AddIfChanged(List<SettingChange> changes, string name, bool before, bool after)
    {
        if (before != after)
        {
            changes.Add(new SettingChange(CommandExecutor.GeneralSection, name, before ? "on" : "off", after ? "on" : "off"));
        }
    }
}

/// <summary>All settings at one moment.</summary>
public sealed class SettingsSnapshot
{
    /// <summary>Creates a snapshot.</summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public SettingsSnapshot(GeneralSettings general, IReadOnlyDictionary<string, ItemSettings> items)
    {
        General = general ?? throw new ArgumentNullException(nameof(general));
        Items = items ?? throw new ArgumentNullException(nameof(items));
    }

    /// <summary>Server-wide settings.</summary>
    public GeneralSettings General { get; }

    /// <summary>Item settings by prefab name.</summary>
    public IReadOnlyDictionary<string, ItemSettings> Items { get; }
}
