using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Inferno.Core.Areas;
using Inferno.Core.Catalog;
using Inferno.Core.Permissions;
using Inferno.Core.Settings;

namespace Inferno.Core.Commands;

/// <summary>Runs parsed <c>!fires</c> commands against the settings store.</summary>
public sealed class CommandExecutor
{
    /// <summary>Config section name of the server-wide settings.</summary>
    public const string GeneralSection = "General";

    /// <summary>How many of their own changes a player can step back through with <c>undo</c>.</summary>
    public const int UndoDepth = 10;

    // Names listed in a reply before "+N more".
    private const int MaxNamesInReply = 4;

    // Help pages: one command per line with a plain description, at most 6 lines each so a page fits the
    // top-left message and reads cleanly in the game's message log (Compendium → Message log).
    private static readonly string[] HelpLines =
    [
        "Inferno: write !fires <command> on a sign or in chat.",
        "alwayson <item> on|off – never needs fuel",
        "burnrate <item> -10..10 – fuel use, 0 = normal",
        "schedule <item> night|day|off – when it's lit",
        "preset night|eternal|vanilla – one-step setups",
        "More: help items · help presets · help admin · status",
    ];

    private static readonly Dictionary<string, string[]> HelpTopics = new(StringComparer.OrdinalIgnoreCase)
    {
        ["items"] =
        [
            "<item> can be:",
            "a name as in game – hot tub, standing wood torch",
            "a word – torches, braziers, fires, lanterns",
            "a group – lights, stations, all",
            "show <item> – its settings · list – everything",
            "add 'nearby' (ward / 20 m) or 'nearby 5' (metres)",
        ],
        ["presets"] =
        [
            "preset eternal [item] – always on (the default)",
            "preset night [item] – lit 18:00-06:00, no fuel used",
            "preset vanilla [item] – normal game behaviour",
            "schedule <item> 18:00 19:00 – your own times",
            "undo – take back your last change",
            "reset <item> – back to defaults",
        ],
        ["admin"] =
        [
            "adminonly on|off – only admins may change settings",
            "hidecommands on|off – hide !fires lines in chat",
            "ignorerain on|off – relight lights after rain",
            "serverownership on|off – experimental, see readme",
            "smoke <item> on|off – client mod players only",
            "F5 console: listkeys fires <command>",
        ],
    };

    private readonly ItemCatalog _catalog;
    private readonly ISettingsStore _store;
    private readonly Func<IReadOnlyList<string>> _status;
    private readonly Dictionary<string, List<UndoEntry>> _undo = new(StringComparer.Ordinal);
    private Func<float?, NearbySelection>? _nearby;

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

    /// <summary>Word that limits a command to the fires around the player or sign (e.g. "torches nearby").</summary>
    public const string NearbyWord = "nearby";

    /// <summary>Runs a command for a player.</summary>
    /// <param name="command">The parsed command.</param>
    /// <param name="sender">Who sent it.</param>
    /// <param name="nearby">
    /// Finds the objects around the sign or player, for commands with "nearby". The argument is the radius the
    /// player gave ("nearby 5"), or null for the default (ward area or 20 m). Null where there is no location
    /// (e.g. the settings menu).
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> or <paramref name="sender"/> is null.</exception>
    public CommandResult Execute(ParsedCommand command, CommandSender sender, Func<float?, NearbySelection>? nearby = null)
    {
        _nearby = nearby;
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
                return Help(command.Name);
            case CommandKind.Status:
                return Ok(_status());
            case CommandKind.List:
            case CommandKind.Show:
                return TrySplitNearby(command.Target!, out var showScope) ? ShowNearby(showScope) : Show(command.Target!);
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
            CommandKind.Reset => TrySplitNearby(command.Target!, out var resetScope)
                ? UpdateObjects(sender, resetScope, "Settings", "item type's", null)
                : UpdateItems(sender, command.Target!, "Settings", "defaults", (i, _) => ItemSettings.DefaultFor(i.Kind)),
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

    private static CommandResult Help(string? topic)
    {
        if (topic is null)
        {
            return Ok(HelpLines);
        }

        return HelpTopics.TryGetValue(topic, out var lines)
            ? Ok(lines)
            : Ok([$"No help topic '{topic}'. Topics: items, presets, admin."]);
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
        if (TrySplitNearby(target, out var scope))
        {
            return UpdateObjects(sender, scope, settingName, newValueText, update);
        }

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
            Remember(sender, UndoEntry.ForItems($"{settingName} = {newValueText} on '{target}'", undo));
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
        Remember(sender, UndoEntry.ForGeneral($"{settingName} = {OnOff(newValue)}", settingName, write, !newValue));
        var change = new SettingChange(GeneralSection, settingName, OnOff(!newValue), OnOff(newValue));
        return new CommandResult([$"{settingName} = {OnOff(newValue)}."], [change], denied: false);
    }

    // Per player: steps back through their own recent changes (not other players'), newest first.
    private CommandResult Undo(CommandSender sender)
    {
        if (!_undo.TryGetValue(sender.PlatformId, out var history) || history.Count == 0)
        {
            return Ok(["Nothing to undo."]);
        }

        var entry = history[history.Count - 1];
        history.RemoveAt(history.Count - 1);
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

        foreach (var obj in entry.Objects)
        {
            var current = _store.GetObject(obj.Key);
            if (!Equals(current, obj.Value))
            {
                _store.SetObject(obj.Key, obj.Value);
                changes.Add(new SettingChange(obj.Key, "Undo", OwnText(current), OwnText(obj.Value)));
            }
        }

        var left = history.Count == 0 ? string.Empty : $" ({history.Count} more to undo)";
        return new CommandResult([$"Undone: {entry.Description}.{left}"], changes, denied: false);
    }

    private void Remember(CommandSender sender, UndoEntry entry)
    {
        if (!_undo.TryGetValue(sender.PlatformId, out var history))
        {
            history = [];
            _undo[sender.PlatformId] = history;
        }

        history.Add(entry);
        if (history.Count > UndoDepth)
        {
            history.RemoveAt(0);
        }
    }

    // "nearby", "nearby 5", "<item> nearby" or "<item> nearby 5": the command applies to the objects around the
    // sign or player only (a number = a circle of that many metres instead of the ward area / 20 m).
    private static bool TrySplitNearby(string target, out NearbyScope scope)
    {
        scope = default;
        var words = target.Split([' '], StringSplitOptions.RemoveEmptyEntries);
        var at = Array.FindLastIndex(words, w => string.Equals(w, NearbyWord, StringComparison.OrdinalIgnoreCase));
        if (at < 0 || at < words.Length - 2)
        {
            return false;
        }

        float? radius = null;
        if (at == words.Length - 2)
        {
            if (!float.TryParse(words[words.Length - 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var metres))
            {
                return false;
            }

            radius = metres;
        }

        scope = new NearbyScope(at == 0 ? null : string.Join(" ", words, 0, at), radius);
        return true;
    }

    // The objects a "nearby" command applies to, or an error line for the reply.
    private bool TrySelectNearby(NearbyScope scope, out List<(NearbyObject Object, CatalogItem Item)> selected, out string area, out string error)
    {
        selected = [];
        area = string.Empty;
        error = string.Empty;
        var filter = scope.Filter;
        if (_nearby is null)
        {
            error = "'nearby' only works on a sign, in chat or in the F5 console.";
            return false;
        }

        if (scope.Radius is { } radius && !(radius >= NearbyArea.MinRadius && radius <= NearbyArea.MaxRadius))
        {
            error = string.Format(CultureInfo.InvariantCulture, "Radius must be {0:0} to {1:0} m.", NearbyArea.MinRadius, NearbyArea.MaxRadius);
            return false;
        }

        HashSet<string>? wanted = null;
        if (filter is not null)
        {
            if (!TryResolve(filter, out var items, out error))
            {
                return false;
            }

            wanted = new HashSet<string>(items.Select(i => i.PrefabName), StringComparer.OrdinalIgnoreCase);
        }

        var selection = _nearby(scope.Radius);
        area = selection.AreaDescription;
        foreach (var obj in selection.Objects)
        {
            if ((wanted is null || wanted.Contains(obj.PrefabName)) && _catalog.TryGet(obj.PrefabName, out var item))
            {
                selected.Add((obj, item));
            }
        }

        if (selected.Count == 0)
        {
            error = $"No {(filter is null ? "fires" : $"'{filter}'")} {area}.";
            return false;
        }

        return true;
    }

    // Changes the objects' own settings. update == null resets them to follow their item type again.
    private CommandResult UpdateObjects(
        CommandSender sender,
        NearbyScope scope,
        string settingName,
        string newValueText,
        Func<CatalogItem, ItemSettings, ItemSettings?>? update)
    {
        if (!TrySelectNearby(scope, out var selected, out var area, out var error))
        {
            return Ok([error]);
        }

        var changes = new List<SettingChange>();
        var undo = new List<KeyValuePair<string, ItemSettings?>>();
        var noAccess = 0;
        var skipped = 0;
        var alwaysOnSwitchedOff = 0;
        foreach (var (obj, item) in selected)
        {
            if (!obj.Allowed)
            {
                noAccess++;
                continue;
            }

            var own = _store.GetObject(obj.Key);
            var before = own ?? _store.GetItem(item.PrefabName);
            ItemSettings? after;
            if (update is null)
            {
                if (own is null)
                {
                    continue;
                }

                after = null;
            }
            else
            {
                after = update(item, before);
                if (after is null)
                {
                    skipped++;
                    continue;
                }

                if (after.Equals(before))
                {
                    continue;
                }

                if (before.AlwaysOn && !after.AlwaysOn && settingName != "AlwaysOn" && settingName != "Preset")
                {
                    alwaysOnSwitchedOff++;
                }
            }

            _store.SetObject(obj.Key, after);
            undo.Add(new KeyValuePair<string, ItemSettings?>(obj.Key, own));
            changes.Add(new SettingChange($"{item.PrefabName} {obj.Key}", settingName, OwnText(own), OwnText(after)));
        }

        var reply = new List<string>
        {
            update is null
                ? $"{changes.Count} of {selected.Count} fire(s) {area} back to their item type's settings."
                : $"{settingName} = {newValueText}: {changes.Count} of {selected.Count} fire(s) {area} changed.",
        };
        if (alwaysOnSwitchedOff > 0)
        {
            reply.Add($"AlwaysOn turned off for {alwaysOnSwitchedOff} of them so this takes effect.");
        }

        if (noAccess > 0)
        {
            reply.Add($"{noAccess} skipped: they're in a ward you have no access to.");
        }

        if (skipped > 0)
        {
            reply.Add($"{skipped} skipped: no on/off switch, so no schedule.");
        }

        if (changes.Count > 0)
        {
            Remember(sender, UndoEntry.ForObjects($"{settingName} = {newValueText} on '{scope}'", undo));
        }

        return new CommandResult(reply, changes, denied: false);
    }

    // One line per item type with the fires' settings; fires with their own settings are marked.
    private CommandResult ShowNearby(NearbyScope scope)
    {
        if (!TrySelectNearby(scope, out var selected, out var area, out var error))
        {
            return Ok([error]);
        }

        var lines = new List<string> { $"{selected.Count} fire(s) {area}:" };
        var groups = selected
            .Select(s => (s.Item, Own: _store.GetObject(s.Object.Key)))
            .Select(s => (s.Item, Settings: s.Own ?? _store.GetItem(s.Item.PrefabName), IsOwn: s.Own is not null))
            .GroupBy(s => (s.Item.DisplayName, Text: s.Settings.ToString(), s.IsOwn))
            .OrderBy(g => g.Key.DisplayName, StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            var own = group.Key.IsOwn ? " (own settings)" : string.Empty;
            lines.Add($"{group.Key.DisplayName} x{group.Count()}{own}: {group.Key.Text}");
        }

        return Ok(lines);
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

    // An object's own settings, or that it has none.
    private static string OwnText(ItemSettings? settings) => settings is null ? "follows item type" : settings.ToString();

    private static CommandResult Ok(IReadOnlyList<string> reply) => new(reply, [], denied: false);

    private static string OnOff(bool value) => value ? "on" : "off";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    // What a "nearby" command selects: an optional item filter and an optional radius.
    private readonly struct NearbyScope(string? filter, float? radius)
    {
        public string? Filter { get; } = filter;

        public float? Radius { get; } = radius;

        public override string ToString()
        {
            var words = Filter is null ? NearbyWord : Filter + " " + NearbyWord;
            return Radius is { } r ? words + " " + r.ToString(CultureInfo.InvariantCulture) : words;
        }
    }

    private sealed class UndoEntry
    {
        private UndoEntry(
            string description,
            IReadOnlyList<KeyValuePair<string, ItemSettings>> items,
            IReadOnlyList<KeyValuePair<string, ItemSettings?>> objects)
        {
            Description = description;
            Items = items;
            Objects = objects;
        }

        public string Description { get; }

        public IReadOnlyList<KeyValuePair<string, ItemSettings>> Items { get; }

        // Objects' own settings before the change (null = it followed its item type).
        public IReadOnlyList<KeyValuePair<string, ItemSettings?>> Objects { get; }

        public string? GeneralSetting { get; private set; }

        public Func<GeneralSettings, bool, GeneralSettings>? WriteGeneral { get; private set; }

        public bool GeneralValue { get; private set; }

        public static UndoEntry ForItems(string description, IReadOnlyList<KeyValuePair<string, ItemSettings>> before) =>
            new(description, before, []);

        public static UndoEntry ForObjects(string description, IReadOnlyList<KeyValuePair<string, ItemSettings?>> before) =>
            new(description, [], before);

        // Only the one setting is restored, so other players' later changes to other settings survive.
        public static UndoEntry ForGeneral(string description, string setting, Func<GeneralSettings, bool, GeneralSettings> write, bool before) =>
            new(description, [], []) { GeneralSetting = setting, WriteGeneral = write, GeneralValue = before };
    }
}
