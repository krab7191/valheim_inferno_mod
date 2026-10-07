using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using BepInEx.Configuration;
using BepInEx.Logging;
using Inferno.Core.Catalog;
using Inferno.Core.Settings;
using Inferno.Core.Sync;
using Inferno.Core.Time;

namespace Inferno.Game;

/// <summary>
/// Settings backed by the BepInEx config file, which is also what the ConfigurationManager (F1) menu shows.
/// </summary>
/// <remarks>
/// On a server: changes from commands are saved immediately; edits to the file (e.g. through a host's web file
/// manager) are picked up live. On a client connected to an Inferno server ("mirror" mode): the entries show the
/// server's values without touching the local file, and menu edits are reported through <see cref="MenuEdited"/>
/// so they can be sent to the server as commands.
/// </remarks>
internal sealed class ConfigSettingsStore : ISettingsStore, IDisposable
{
    private const string GeneralSection = "General";

    private readonly ConfigFile _config;
    private readonly ManualLogSource _log;
    private readonly ConfigEntry<bool> _adminOnly;
    private readonly ConfigEntry<bool> _hideCommands;
    private readonly ConfigEntry<bool> _ignoreRain;
    private readonly ConfigEntry<bool> _serverOwnership;
    private readonly Dictionary<string, ItemEntries> _items = new(StringComparer.OrdinalIgnoreCase);

    // One shared instance: ConfigurationManager reads it by type name, so flipping ReadOnly locks every Inferno entry.
    private readonly ConfigurationManagerAttributes _menu = new();
    private FileSystemWatcher? _watcher;
    private int _fileChanged;
    private DateTime _ignoreChangesUntil;
    private SettingsMessage? _remote;
    private bool _suppressEvents;
    private bool _saveOnSetBeforeMirror;

    public ConfigSettingsStore(ConfigFile config, ManualLogSource log)
    {
        _config = config;
        _log = log;
        _adminOnly = Bind(GeneralSection, "AdminOnly", GeneralSettings.Default.AdminOnly,
            "Only admins (the server's adminlist.txt) may change Inferno settings. Anyone may turn this on; only admins may turn it off.");
        _hideCommands = Bind(GeneralSection, "HideCommands", GeneralSettings.Default.HideCommands,
            "Hide '!fires' chat commands from other players.");
        _ignoreRain = Bind(GeneralSection, "IgnoreRain", GeneralSettings.Default.IgnoreRain,
            "Switch lights back on after rain or wind put them out. Note: this also relights lights a player switched off by hand.");
        _serverOwnership = Bind(GeneralSection, "ServerOwnership", GeneralSettings.Default.ServerOwnership,
            "EXPERIMENTAL. The server keeps ownership of fires, so no player's game burns their fuel or switches them off in rain. "
            + "Burn rate and schedule become exact. Side effects: no sound when adding fuel or switching these fires; they don't go out in rain. "
            + "Fires that melt snow or spread fire are excluded. Turning it off gives the fires back immediately.");
        _config.SettingChanged += OnSettingChanged;
    }

    /// <summary>Raised on a client when the player edits a mirrored setting; carries the config section.</summary>
    public event Action<string>? MenuEdited;

    /// <summary>Path of the config file.</summary>
    public string ConfigPath => _config.ConfigFilePath;

    /// <summary>True while the entries show a remote server's settings.</summary>
    public bool IsMirroring => _remote is not null;

    public GeneralSettings General
    {
        get => new(_adminOnly.Value, _hideCommands.Value, _ignoreRain.Value, _serverOwnership.Value);
        set
        {
            Saving();
            _adminOnly.Value = value.AdminOnly;
            _hideCommands.Value = value.HideCommands;
            _ignoreRain.Value = value.IgnoreRain;
            _serverOwnership.Value = value.ServerOwnership;
        }
    }

    /// <summary>Creates the config entries for every catalog item (existing values in the file are kept).</summary>
    public void BindItems(ItemCatalog catalog)
    {
        // Bind without saving after each entry, then save once (never while mirroring a server).
        var saveOnSet = _config.SaveOnConfigSet;
        _config.SaveOnConfigSet = false;
        try
        {
            foreach (var item in catalog.Items)
            {
                if (!_items.ContainsKey(item.PrefabName))
                {
                    _items.Add(item.PrefabName, ItemEntries.Bind(this, item));
                }
            }
        }
        finally
        {
            _config.SaveOnConfigSet = saveOnSet;
        }

        if (!IsMirroring)
        {
            Saving();
            _config.Save();
        }
    }

    public ItemSettings GetItem(string prefabName) =>
        _items.TryGetValue(prefabName, out var entries) ? entries.Read(_log) : ItemSettings.Vanilla;

    public void SetItem(string prefabName, ItemSettings settings)
    {
        if (_items.TryGetValue(prefabName, out var entries))
        {
            Saving();
            entries.Write(settings);
        }
    }

    public SettingsSnapshot Snapshot()
    {
        var items = new Dictionary<string, ItemSettings>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _items)
        {
            items[entry.Key] = entry.Value.Read(_log);
        }

        return new SettingsSnapshot(General, items);
    }

    /// <summary>Client: shows a server's settings in the entries (and menu) without writing the local file.</summary>
    public void ApplyRemote(SettingsMessage message)
    {
        if (_remote is null)
        {
            _saveOnSetBeforeMirror = _config.SaveOnConfigSet;
            _config.SaveOnConfigSet = false;
        }

        _suppressEvents = true;
        try
        {
            General = message.General;
            foreach (var item in message.Items)
            {
                if (_items.TryGetValue(item.Key, out var entries))
                {
                    entries.Write(item.Value);
                }
            }

            _menu.ReadOnly = !message.CanEdit;
            _remote = message;
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    /// <summary>Client: leaves mirror mode and restores the local file's values.</summary>
    public void StopMirroring()
    {
        if (_remote is null)
        {
            return;
        }

        _suppressEvents = true;
        try
        {
            _config.Reload();
            _config.SaveOnConfigSet = _saveOnSetBeforeMirror;
            _menu.ReadOnly = false;
            _remote = null;
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    /// <summary>Starts watching the config file for edits made outside the game.</summary>
    public void WatchFile()
    {
        var path = _config.ConfigFilePath;
        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
        };

        // Raised on a worker thread: only set a flag; the reload happens on the main thread.
        _watcher.Changed += (_, _) => Interlocked.Exchange(ref _fileChanged, 1);
        _watcher.Created += (_, _) => Interlocked.Exchange(ref _fileChanged, 1);
        _watcher.Renamed += (_, _) => Interlocked.Exchange(ref _fileChanged, 1);
        _watcher.EnableRaisingEvents = true;
    }

    /// <summary>Reloads the file if it was edited. Call from the main thread. Returns true when reloaded.</summary>
    public bool ReloadIfChanged()
    {
        if (Interlocked.Exchange(ref _fileChanged, 0) == 0 || DateTime.UtcNow < _ignoreChangesUntil)
        {
            return false;
        }

        try
        {
            _config.Reload();
            return true;
        }
        catch (IOException e)
        {
            // The editor may still be writing; try again on the next change notification or poll.
            _log.LogWarning($"Could not reload config yet ({e.Message}); will retry.");
            Interlocked.Exchange(ref _fileChanged, 1);
            return false;
        }
    }

    /// <summary>Stops watching the file. The store itself stays usable (it lives as long as the plugin).</summary>
    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    private ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description, AcceptableValueBase? range = null) =>
        _config.Bind(section, key, defaultValue, new ConfigDescription(description, range, _menu));

    /// <summary>Client: the commands that turn the server's values of a section into the menu's current values.</summary>
    public IReadOnlyList<string> CommandsFor(string section)
    {
        var remote = _remote;
        if (remote is null)
        {
            return [];
        }

        if (section == GeneralSection)
        {
            return MenuCommands.ForGeneral(remote.General, General);
        }

        return remote.Items.TryGetValue(section, out var before) ? MenuCommands.ForItem(section, before, GetItem(section)) : [];
    }

    private void OnSettingChanged(object sender, SettingChangedEventArgs e)
    {
        if (!_suppressEvents && _remote is not null)
        {
            MenuEdited?.Invoke(e.ChangedSetting.Definition.Section);
        }
    }

    // Our own saves also trigger the watcher; ignore notifications shortly after we write.
    private void Saving() => _ignoreChangesUntil = DateTime.UtcNow.AddSeconds(2);

    private sealed class ItemEntries
    {
        private ConfigEntry<bool> _alwaysOn = null!;
        private ConfigEntry<int> _burnRate = null!;
        private ConfigEntry<bool> _smoke = null!;
        private ConfigEntry<int>? _onHour;
        private ConfigEntry<int>? _onMinute;
        private ConfigEntry<int>? _offHour;
        private ConfigEntry<int>? _offMinute;

        public static ItemEntries Bind(ConfigSettingsStore store, CatalogItem item)
        {
            var defaults = ItemSettings.DefaultFor(item.Kind);
            var section = item.PrefabName;
            var kind = item.Kind == ItemKind.LightSource ? "light source" : "fuel station (no on/off switch, so no schedule)";
            var entries = new ItemEntries
            {
                _alwaysOn = store.Bind(section, "AlwaysOn", defaults.AlwaysOn,
                    $"{item.DisplayName} — {kind}. Keep fuel full at all times. While on, burn rate and schedule are kept but have no effect (setting either by command switches this off)."),
                _burnRate = store.Bind(section, "BurnRate", defaults.BurnRateLevel,
                    "Fuel burn rate: 0 = vanilla, each step is 10 %. -10 = uses no fuel, 10 = burns twice as fast.",
                    new AcceptableValueRange<int>(BurnRate.Min, BurnRate.Max)),
                _smoke = store.Bind(section, "Smoke", defaults.Smoke,
                    "Whether this item makes smoke. Only players with the Inferno client mod see the smoke removed; others see vanilla smoke."),
            };

            if (item.CanSchedule)
            {
                var hours = new AcceptableValueRange<int>(0, ClockSetting.MaxHour);
                var minutes = new AcceptableValueRange<int>(0, ClockSetting.MaxMinute);
                const string Note = "In-game clock (06:00 = sunrise, 18:00 = sunset). On time = off time means no schedule. Only used when AlwaysOn is false.";
                entries._onHour = store.Bind(section, "OnTimeHour", 0, $"Hour the light turns on. {Note}", hours);
                entries._onMinute = store.Bind(section, "OnTimeMinute", 0, "Minute the light turns on.", minutes);
                entries._offHour = store.Bind(section, "OffTimeHour", 0, "Hour the light turns off.", hours);
                entries._offMinute = store.Bind(section, "OffTimeMinute", 0, "Minute the light turns off.", minutes);
            }

            return entries;
        }

        public ItemSettings Read(ManualLogSource log)
        {
            try
            {
                var schedule = _onHour is null
                    ? DailySchedule.AlwaysOn
                    : new DailySchedule(
                        ClockSetting.ToTimeOfDay(_onHour.Value, _onMinute!.Value),
                        ClockSetting.ToTimeOfDay(_offHour!.Value, _offMinute!.Value));
                return new ItemSettings(_alwaysOn.Value, _burnRate.Value, schedule, _smoke.Value);
            }
            catch (ArgumentOutOfRangeException e)
            {
                // BepInEx clamps ranged values, so this only happens if the file is edited mid-read.
                log.LogWarning($"Invalid value in config section [{_alwaysOn.Definition.Section}]: {e.Message} Using vanilla until fixed.");
                return ItemSettings.Vanilla;
            }
        }

        public void Write(ItemSettings settings)
        {
            _alwaysOn.Value = settings.AlwaysOn;
            _burnRate.Value = settings.BurnRateLevel;
            _smoke.Value = settings.Smoke;
            if (_onHour is not null)
            {
                _onHour.Value = settings.Schedule.OnTime.Hour;
                _onMinute!.Value = settings.Schedule.OnTime.Minute;
                _offHour!.Value = settings.Schedule.OffTime.Hour;
                _offMinute!.Value = settings.Schedule.OffTime.Minute;
            }
        }
    }
}
