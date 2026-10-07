using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Inferno.Core.Catalog;
using Inferno.Core.Permissions;
using Inferno.Core.Settings;

namespace Inferno.Core.Commands;

/// <summary>Runs parsed <c>!fires</c> commands against the settings store.</summary>
public sealed class CommandExecutor
{
    /// <summary>Config section name of the server-wide settings.</summary>
    public const string GeneralSection = "General";

    // Names listed in a reply before "+N more".
    private const int MaxNamesInReply = 4;

    private static readonly string[] HelpLines =
    [
        "Inferno: type '!fires <command>' in chat or on a sign, or 'listkeys fires <command>' in the F5 console.",
        "<item> = name as in game (hot tub, standing wood torch), a word (torches, braziers, fires), or all / lights / stations.",
        "preset eternal|night|vanilla [item]  ·  undo  ·  status  ·  list  ·  show <item>  ·  reset <item>",
        "alwayson <item> on|off  ·  burnrate <item> <-10..10> (0 = vanilla, 10 % per step)",
        "schedule <item> night|day|off  or  schedule <item> <on HH:MM> <off HH:MM>",
        "smoke <item> on|off (client mod only)  ·  adminonly / hidecommands / ignorerain / serverownership on|off",
    ];

    private readonly ItemCatalog _catalog;
    private readonly ISettingsStore _store;
    private readonly Func<IReadOnlyList<string>> _status;
    private readonly Dictionary<string, UndoEntry> _undo = new(StringComparer.Ordinal);

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
            CommandKind.AlwaysOn => UpdateItems(sender, command.Target!, "AlwaysOn", OnOff(command.Flag), (_, s) => s.WithAlwaysOn(command.Flag)),
            CommandKind.Smoke => UpdateItems(sender, command.Target!, "Smoke", OnOff(command.Flag), (_, s) => s.WithSmoke(command.Flag)),
            // Always on would hide a new burn rate or schedule window, so setting one switches always on off. The
            // stored values survive the other way round: turning always on back on keeps them for later.
            CommandKind.BurnRate => UpdateItems(sender, command.Target!, "BurnRate", Number(command.Number), (_, s) => s.WithBurnRate(command.Number).WithAlwaysOn(false)),
            CommandKind.Schedule => UpdateItems(
                sender,
                command.Target!,
                "Schedule",
                command.Schedule.ToString(),
                (i, s) => !i.CanSchedule ? null : command.Schedule.IsAlwaysOn ? s.WithSchedule(command.Schedule) : s.WithSchedule(command.Schedule).WithAlwaysOn(false)),
            CommandKind.Reset => UpdateItems(sender, command.Target!, "Settings", "defaults", (i, _) => ItemSettings.DefaultFor(i.Kind)),
            CommandKind.Preset => ApplyPreset(sender, command),
            CommandKind.Undo => Undo(sender),
            CommandKind.AdminOnly => UpdateGeneral(sender, "AdminOnly", g => g.AdminOnly, (g, v) => g.WithAdminOnly(v), command.Flag),
            CommandKind.HideCommands => UpdateGeneral(sender, "HideCommands", g => g.HideCommands, (g, v) => g.WithHideCommands(v), command.Flag),
            CommandKind.IgnoreRain => UpdateGeneral(sender, "IgnoreRain", g => g.IgnoreRain, (g, v) => g.WithIgnoreRain(v), command.Flag),
            _ => UpdateGeneral(sender, "ServerOwnership", g => g.ServerOwnership, (g, v) => g.WithServerOwnership(v), command.Flag),
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

    private CommandResult ApplyPreset(CommandSender sender, ParsedCommand command)
    {
        if (!Presets.TryGet(command.Name, out var preset))
        {
            var names = string.Join(", ", Presets.All.Select(p => $"{p.Name} ({p.Description})"));
            return Ok([$"Unknown preset '{command.Name}'. Presets: {names}."]);
        }

        return UpdateItems(sender, command.Target ?? preset.DefaultTarget, "Preset", preset.Name, preset.Apply);
    }

    private CommandResult UpdateItems(
        CommandSender sender,
        string target,
        string settingName,
        string newValueText,
        Func<CatalogItem, ItemSettings, ItemSettings?> update)
    {
        if (!TryResolve(target, out var items, out var error))
        {
            return Ok([error]);
        }

        var changes = new List<SettingChange>();
        var undo = new List<KeyValuePair<string, ItemSettings>>();
        var changedNames = new List<string>();
        var skipped = 0;
        var alwaysOnSwitchedOff = 0;
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
                if (before.AlwaysOn && !after.AlwaysOn && settingName != "AlwaysOn" && settingName != "Preset" && settingName != "Settings")
                {
                    alwaysOnSwitchedOff++;
                }

                _store.SetItem(item.PrefabName, after);
                changes.Add(new SettingChange(item.PrefabName, settingName, before.ToString(), after.ToString()));
                undo.Add(new KeyValuePair<string, ItemSettings>(item.PrefabName, before));
                changedNames.Add(item.DisplayName);
            }
        }

        var reply = new List<string>
        {
            $"{settingName} = {newValueText}: {changes.Count} of {items.Count} item(s) changed.",
        };

        // A word like "fires" can select more than the player expects: say exactly what changed.
        if (changedNames.Count > 1 && !IsFixedGroup(target))
        {
            reply.Add("Changed: " + NameList(changedNames));
        }

        if (alwaysOnSwitchedOff > 0)
        {
            reply.Add($"AlwaysOn turned off for {alwaysOnSwitchedOff} item(s) so this takes effect.");
        }

        if (skipped > 0)
        {
            reply.Add($"{skipped} item(s) skipped: they have no on/off switch and can't follow a schedule.");
        }

        if (changes.Count > 0)
        {
            _undo[sender.PlatformId] = UndoEntry.ForItems($"{settingName} = {newValueText} on '{target}'", undo);
        }

        return new CommandResult(reply, changes, denied: false);
    }

    private CommandResult UpdateGeneral(
        CommandSender sender,
        string settingName,
        Func<GeneralSettings, bool> read,
        Func<GeneralSettings, bool, GeneralSettings> write,
        bool newValue)
    {
        var before = _store.General;
        if (read(before) == newValue)
        {
            return Ok([$"{settingName} is already {OnOff(newValue)}."]);
        }

        _store.General = write(before, newValue);
        _undo[sender.PlatformId] = UndoEntry.ForGeneral($"{settingName} = {OnOff(newValue)}", settingName, write, !newValue);
        var change = new SettingChange(GeneralSection, settingName, OnOff(!newValue), OnOff(newValue));
        return new CommandResult([$"{settingName} = {OnOff(newValue)}."], [change], denied: false);
    }

    // One step back, per player: their own most recent change (not other players').
    private CommandResult Undo(CommandSender sender)
    {
        if (!_undo.TryGetValue(sender.PlatformId, out var entry))
        {
            return Ok(["Nothing to undo."]);
        }

        _undo.Remove(sender.PlatformId);
        var changes = new List<SettingChange>();
        if (entry.GeneralSetting is { } setting)
        {
            var current = _store.General;
            _store.General = entry.WriteGeneral!(current, entry.GeneralValue);
            changes.Add(new SettingChange(GeneralSection, setting, OnOff(!entry.GeneralValue), OnOff(entry.GeneralValue)));
        }

        foreach (var item in entry.Items)
        {
            var current = _store.GetItem(item.Key);
            if (!current.Equals(item.Value))
            {
                _store.SetItem(item.Key, item.Value);
                changes.Add(new SettingChange(item.Key, "Undo", current.ToString(), item.Value.ToString()));
            }
        }

        return new CommandResult([$"Undone: {entry.Description}."], changes, denied: false);
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

    private static bool IsFixedGroup(string target) =>
        string.Equals(target, ItemCatalog.AllGroup, StringComparison.OrdinalIgnoreCase)
        || string.Equals(target, ItemCatalog.LightsGroup, StringComparison.OrdinalIgnoreCase)
        || string.Equals(target, ItemCatalog.StationsGroup, StringComparison.OrdinalIgnoreCase);

    private static string NameList(List<string> names)
    {
        var distinct = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var shown = string.Join(", ", distinct.Take(MaxNamesInReply));
        return distinct.Count > MaxNamesInReply ? $"{shown}, +{distinct.Count - MaxNamesInReply} more" : shown;
    }

    private static CommandResult Ok(IReadOnlyList<string> reply) => new(reply, [], denied: false);

    private static string OnOff(bool value) => value ? "on" : "off";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private sealed class UndoEntry
    {
        private UndoEntry(string description, IReadOnlyList<KeyValuePair<string, ItemSettings>> items)
        {
            Description = description;
            Items = items;
        }

        public string Description { get; }

        public IReadOnlyList<KeyValuePair<string, ItemSettings>> Items { get; }

        public string? GeneralSetting { get; private set; }

        public Func<GeneralSettings, bool, GeneralSettings>? WriteGeneral { get; private set; }

        public bool GeneralValue { get; private set; }

        public static UndoEntry ForItems(string description, IReadOnlyList<KeyValuePair<string, ItemSettings>> before) =>
            new(description, before);

        // Only the one setting is restored, so other players' later changes to other settings survive.
        public static UndoEntry ForGeneral(string description, string setting, Func<GeneralSettings, bool, GeneralSettings> write, bool before) =>
            new(description, []) { GeneralSetting = setting, WriteGeneral = write, GeneralValue = before };
    }
}
