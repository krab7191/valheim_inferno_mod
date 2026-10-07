using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using Inferno.Core.Commands;

namespace Inferno.Game;

/// <summary>Everything Inferno runs while a world is loaded on a server.</summary>
internal sealed class InfernoRuntime : IDisposable
{
    private readonly SyncServer _sync;
    private readonly PrefabDiscovery.Result _discovery;

    private InfernoRuntime(ManualLogSource log, ConfigSettingsStore store, CommandService commands, FuelScanner scanner, ServerOwnership ownership, SyncServer sync, PrefabDiscovery.Result discovery)
    {
        _sync = sync;
        _discovery = discovery;
        Log = log;
        Store = store;
        Commands = commands;
        Scanner = scanner;
        Ownership = ownership;
    }

    /// <summary>The running instance, or null when no server world is loaded.</summary>
    public static InfernoRuntime? Current { get; private set; }

    public ManualLogSource Log { get; }

    public ConfigSettingsStore Store { get; }

    public CommandService Commands { get; }

    public FuelScanner Scanner { get; }

    public ServerOwnership Ownership { get; }

    public static void Start(ConfigSettingsStore store, ManualLogSource log, HarmonyLib.Harmony? harmony)
    {
        Diagnostics.LogStartup(harmony, store.ConfigPath);
        var discovery = PrefabDiscovery.Run(ZNetScene.instance, log);
        store.BindItems(discovery.Catalog);
        store.WatchFile();

        var commands = new CommandService(new CommandExecutor(discovery.Catalog, store, () => Current?.StatusLines() ?? []), store, log);
        var ownership = new ServerOwnership(discovery, store, log);
        var scanner = new FuelScanner(discovery, store, commands, ownership, log);
        var sync = new SyncServer(store, commands, log);
        sync.Register(ZRoutedRpc.instance);
        Current = new InfernoRuntime(log, store, commands, scanner, ownership, sync, discovery);

        var general = store.General;
        log.LogInfo($"Running on server. AdminOnly={general.AdminOnly}, HideCommands={general.HideCommands}, IgnoreRain={general.IgnoreRain}, ServerOwnership={general.ServerOwnership}.");
    }

    public static void Stop()
    {
        if (Current is null)
        {
            return;
        }

        try
        {
            Current.Scanner.ReleaseOwnership();
        }
        finally
        {
            Current.Dispose();
            Current = null;
        }
    }

    public void Tick(float now)
    {
        if (Store.ReloadIfChanged())
        {
            Commands.OnConfigReloaded();
        }

        Scanner.Tick(now);
        _sync.Tick(now);
    }

    /// <summary>Lines for <c>!fires status</c>: enough for a remote tester to confirm Inferno works.</summary>
    public IReadOnlyList<string> StatusLines()
    {
        var general = Store.General;
        var lights = 0;
        foreach (var item in _discovery.Catalog.Items)
        {
            if (item.Kind == Inferno.Core.Settings.ItemKind.LightSource)
            {
                lights++;
            }
        }

        var gameVersion = global::Version.CurrentVersion.ToString();
        return
        [
            $"Inferno {MyPluginInfo.PLUGIN_VERSION} running on Valheim {gameVersion}.",
            $"Item types: {lights} light(s), {_discovery.Catalog.Items.Count - lights} station(s). {Scanner.StatusText}",
            $"In-game time {Scanner.CurrentTimeText}. AdminOnly {OnOff(general.AdminOnly)}, IgnoreRain {OnOff(general.IgnoreRain)}, ServerOwnership {OnOff(general.ServerOwnership)}.",
            Diagnostics.ErrorCount == 0 ? "Errors since start: 0." : $"Errors since start: {Diagnostics.ErrorCount} (see the server log).",
        ];
    }

    private static string OnOff(bool value) => value ? "on" : "off";

    public void Dispose()
    {
        _sync.Unregister();
        Store.Dispose();
    }
}
