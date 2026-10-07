using System;
using System.Collections.Generic;
using System.Globalization;
using Inferno.Core.Catalog;
using Inferno.Core.Permissions;
using Inferno.Core.Settings;

namespace Inferno.Core.Commands;

/// <summary>Runs parsed <c>!fires</c> commands against the settings store.</summary>
public sealed class CommandExecutor
{
    /// <summary>Config section name of the server-wide settings.</summary>
    public const string GeneralSection = "General";

    private static readonly string[] HelpLines =
    [
        "Inferno: type '!fires <command>' in chat or on a sign, or 'listkeys fires <command>' in the F5 console.",
        "<item> = the name as in game (e.g. hot tub, standing wood torch), or: all, lights, stations.",
        "status  ·  list [all|lights|stations]  ·  show <item>  ·  reset <item>",
        "alwayson <item> on|off  ·  smoke <item> on|off (client mod only)",
        "burnrate <item> <-10..10>   (0 = vanilla, each step 10 %)",
        "schedule <item> <on HH:MM> <off HH:MM>   |   schedule <item> off",
        "adminonly on|off  ·  hidecommands on|off  ·  ignorerain on|off  ·  serverownership on|off",
    ];

    private readonly ItemCatalog _catalog;
    private readonly ISettingsStore _store;
    private readonly Func<IReadOnlyList<string>> _status;

    /// <summary>Creates an executor.</summary>
    /// <param name="catalog">All known items.</param>
    /// <param name="store">Settings storage.</param>
    /// <param name="status">Supplies the lines for <c>status</c> (runtime facts the core can't know); optional.</param>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> or <paramref name="store"/> is null.</exception>
    public CommandExecutor(ItemCatalog catalog, ISettingsStore store, Func<IReadOnlyList<string>>? status = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _status = status ?? (() => ["Inferno is running."]);
    }

    /// <summary>Runs a command for a player.</summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public CommandResult Execute(ParsedCommand command, CommandSender sender)
    {
        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (sender is null)
        {
            throw new ArgumentNullException(nameof(sender));
        }

        switch (command.Kind)
        {
            case CommandKind.Help:
                return Ok(HelpLines);
            case CommandKind.Status:
                return Ok(_status());
            case CommandKind.List:
            case CommandKind.Show:
                return Show(command.Target!);
        }

        if (!PermissionPolicy.CanChangeSettings(sender.IsAdmin, _store.General.AdminOnly))
        {
            return new CommandResult(["Only admins can change Inferno settings while AdminOnly is on."], [], denied: true);
        }

        return command.Kind switch
        {
            CommandKind.AlwaysOn => UpdateItems(command.Target!, "AlwaysOn", OnOff(command.Flag), (_, s) => s.WithAlwaysOn(command.Flag)),
            CommandKind.Smoke => UpdateItems(command.Target!, "Smoke", OnOff(command.Flag), (_, s) => s.WithSmoke(command.Flag)),
            CommandKind.BurnRate => UpdateItems(command.Target!, "BurnRate", Number(command.Number), (_, s) => s.WithBurnRate(command.Number)),
            CommandKind.Schedule => UpdateItems(command.Target!, "Schedule", command.Schedule.ToString(), (i, s) => i.CanSchedule ? s.WithSchedule(command.Schedule) : null),
            CommandKind.Reset => UpdateItems(command.Target!, "Settings", "defaults", (i, _) => ItemSettings.DefaultFor(i.Kind)),
            CommandKind.AdminOnly => UpdateGeneral("AdminOnly", g => g.AdminOnly, g => g.WithAdminOnly(command.Flag), command.Flag),
            CommandKind.HideCommands => UpdateGeneral("HideCommands", g => g.HideCommands, g => g.WithHideCommands(command.Flag), command.Flag),
            CommandKind.IgnoreRain => UpdateGeneral("IgnoreRain", g => g.IgnoreRain, g => g.WithIgnoreRain(command.Flag), command.Flag),
            _ => UpdateGeneral("ServerOwnership", g => g.ServerOwnership, g => g.WithServerOwnership(command.Flag), command.Flag),
        };
    }

    /// <summary>Describes one item and its settings in a single line.</summary>
    public string Describe(CatalogItem item)
    {
        if (item is null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        var s = _store.GetItem(item.PrefabName);
        var kind = item.Kind == ItemKind.LightSource ? "light" : "station";
        var schedule = item.CanSchedule ? s.Schedule.ToString() : "n/a";
        return $"{item.DisplayName} ({item.PrefabName}, {kind}): alwayson={OnOff(s.AlwaysOn)} burnrate={Number(s.BurnRateLevel)} schedule={schedule} smoke={OnOff(s.Smoke)}";
    }

    private CommandResult Show(string target)
    {
        if (!TryResolve(target, out var items, out var error))
        {
            return Ok([error]);
        }

        var lines = new List<string>(items.Count);
        foreach (var item in items)
        {
            lines.Add(Describe(item));
        }

        return Ok(lines);
    }

    private CommandResult UpdateItems(string target, string settingName, string newValueText, Func<CatalogItem, ItemSettings, ItemSettings?> update)
    {
        if (!TryResolve(target, out var items, out var error))
        {
            return Ok([error]);
        }

        var changes = new List<SettingChange>();
        var skipped = 0;
        foreach (var item in items)
        {
            var before = _store.GetItem(item.PrefabName);
            var after = update(item, before);
            if (after is null)
            {
                skipped++;
                continue;
            }

            if (!after.Equals(before))
            {
                _store.SetItem(item.PrefabName, after);
                changes.Add(new SettingChange(item.PrefabName, settingName, before.ToString(), after.ToString()));
            }
        }

        var reply = new List<string>
        {
            $"{settingName} = {newValueText}: {changes.Count} of {items.Count} item(s) changed.",
        };
        if (skipped > 0)
        {
            reply.Add($"{skipped} item(s) skipped: they have no on/off switch and can't follow a schedule.");
        }

        return new CommandResult(reply, changes, denied: false);
    }

    private CommandResult UpdateGeneral(string settingName, Func<GeneralSettings, bool> read, Func<GeneralSettings, GeneralSettings> update, bool newValue)
    {
        var before = _store.General;
        if (read(before) == newValue)
        {
            return Ok([$"{settingName} is already {OnOff(newValue)}."]);
        }

        _store.General = update(before);
        var change = new SettingChange(GeneralSection, settingName, OnOff(!newValue), OnOff(newValue));
        return new CommandResult([$"{settingName} = {OnOff(newValue)}."], [change], denied: false);
    }

    private bool TryResolve(string target, out IReadOnlyList<CatalogItem> items, out string error)
    {
        error = string.Empty;
        if (!_catalog.TryResolve(target, out items))
        {
            var suggestions = _catalog.Suggest(target);
            error = suggestions.Count > 0
                ? $"Unknown item '{target}'. Did you mean: {string.Join(", ", suggestions)}?"
                : $"Unknown item '{target}'. Use 'list' to see all items.";
            return false;
        }

        if (items.Count == 0)
        {
            error = $"No items in '{target}'.";
            return false;
        }

        return true;
    }

    private static CommandResult Ok(IReadOnlyList<string> reply) => new(reply, [], denied: false);

    private static string OnOff(bool value) => value ? "on" : "off";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
