using System;
using System.Linq;
using System.Text;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using Inferno.Core.Diagnostics;
using UnityEngine;
using ValheimVersion = global::Version;

namespace Inferno.Game;

/// <summary>
/// Error reporting and startup diagnostics. On a hosted server the BepInEx log is often the only evidence, so every
/// error is reported once in full (with stack trace and context), repeats are summarised instead of flooding the
/// log, and the startup block records everything needed to reproduce a problem.
/// </summary>
internal static class Diagnostics
{
    private const double RepeatSummarySeconds = 300;

    private static readonly ErrorThrottle Throttle = new(RepeatSummarySeconds);
    private static ManualLogSource? _log;

    /// <summary>Errors since the game started (shown by <c>!fires status</c>).</summary>
    public static int ErrorCount => Throttle.Total;

    public static void Init(ManualLogSource log) => _log = log;

    /// <summary>
    /// Reports an error that Inferno recovered from. <paramref name="where"/> names the operation (and, where
    /// useful, the object), e.g. "applying settings to piece_bathtub".
    /// </summary>
    public static void Error(string where, Exception e)
    {
        var decision = Throttle.Record(where + ":" + e.GetType().FullName, Time.realtimeSinceStartup);
        if (!decision.Log || _log is null)
        {
            return;
        }

        if (decision.FirstTime)
        {
            _log.LogError(
                $"Error while {where}. Inferno skipped this and keeps running. Please report it with this log "
                + $"(Inferno {MyPluginInfo.PLUGIN_VERSION}, Valheim {ValheimVersion.CurrentVersion}):\n{e}");
        }
        else
        {
            _log.LogError(
                $"Error while {where} happened again ({decision.Suppressed} more time(s) in the last "
                + $"{RepeatSummarySeconds / 60:0} min): {e.GetType().Name}: {e.Message}");
        }
    }

    /// <summary>Logs the environment once when Inferno starts on a server. Never throws.</summary>
    public static void LogStartup(Harmony? harmony, string configPath)
    {
        if (_log is null)
        {
            return;
        }

        try
        {
            WriteStartup(harmony, configPath);
        }
        catch (Exception e)
        {
            // Diagnostics must never stop Inferno from starting.
            Error("writing the startup diagnostics", e);
        }
    }

    private static void WriteStartup(Harmony? harmony, string configPath)
    {

        var net = ZNet.instance;
        var sb = new StringBuilder();
        sb.AppendLine("--- Inferno startup diagnostics (include this when reporting a problem) ---");
        sb.AppendLine($"Inferno {MyPluginInfo.PLUGIN_VERSION} | Valheim {ValheimVersion.CurrentVersion} (network {ValheimVersion.c_networkVersion}) | BepInEx {typeof(Chainloader).Assembly.GetName().Version}");
        sb.AppendLine($"Server: {(net.IsDedicated() ? "dedicated" : "hosted from game")} | crossplay {(ZNet.m_onlineBackend == OnlineBackendType.PlayFab ? "on" : "off")} | world '{net.GetWorldName()}' | OS {Environment.OSVersion} ({Application.platform})");
        sb.AppendLine($"Config: {configPath}");

        var others = Chainloader.PluginInfos.Values
            .Where(p => p.Metadata.GUID != MyPluginInfo.PLUGIN_GUID)
            .Select(p => $"{p.Metadata.Name} {p.Metadata.Version}")
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
        sb.AppendLine(others.Count == 0 ? "Other mods: none" : $"Other mods ({others.Count}): {string.Join(", ", others)}");
        sb.Append(SharedHooks(harmony));
        _log!.LogInfo(sb.ToString().TrimEnd());
    }

    // Another mod patching the same game method is the most likely cause of a conflict; name them up front.
    private static string SharedHooks(Harmony? harmony)
    {
        if (harmony is null)
        {
            return "Shared hooks: unknown";
        }

        var shared = new StringBuilder();
        foreach (var method in harmony.GetPatchedMethods())
        {
            var info = Harmony.GetPatchInfo(method);
            var owners = info?.Owners.Where(o => o != harmony.Id).ToList();
            if (owners is { Count: > 0 })
            {
                shared.Append($"{method.DeclaringType?.Name}.{method.Name} (also patched by {string.Join(", ", owners)}); ");
            }
        }

        return shared.Length == 0
            ? "Shared hooks: none (no other mod patches the game methods Inferno uses)"
            : "Shared hooks: " + shared.ToString().TrimEnd(' ', ';');
    }
}
