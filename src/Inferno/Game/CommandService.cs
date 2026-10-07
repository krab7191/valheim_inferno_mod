using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Inferno.Core.Commands;
using Inferno.Core.Settings;
using UnityEngine;

namespace Inferno.Game;

/// <summary>Runs Inferno commands from chat, the F5 console and signs, and writes the audit log.</summary>
internal sealed class CommandService(CommandExecutor executor, ConfigSettingsStore store, ManualLogSource log)
{
    // A chat line arrives once per other player online; run it once.
    private readonly DuplicateFilter _chatCopies = new(windowSeconds: 3.0);

    // Shared by every source, keyed by platform id: a burst of 10, then one command per second.
    private readonly RateLimiter _limiter = new(burst: 10, refillPerSecond: 1.0);
    private SettingsSnapshot _snapshot = store.Snapshot();

    /// <summary>Raised after any setting changed (command, menu or config-file edit).</summary>
    public event Action? SettingsChanged;

    public bool HideCommands => store.General.HideCommands;

    /// <summary>Handles a command sent by a player's settings menu (Inferno client mod).</summary>
    public void HandleMenu(long senderUid, string text)
    {
        var peer = Players.FindPeer(senderUid);
        if (peer is null)
        {
            return;
        }

        var outcome = CommandParser.Parse(text, out var command, out var error);
        if (outcome == ParseOutcome.NotACommand)
        {
            log.LogDebug($"Ignoring malformed menu message from {Players.FromPeer(peer)}.");
            return;
        }

        var reply = Run("menu", Players.FromPeer(peer), text, outcome, command, error, out var denied);
        Players.ShowMessage(peer, reply);
        if (denied || outcome == ParseOutcome.Invalid)
        {
            // Resend the real values so the player's menu snaps back.
            SettingsChanged?.Invoke();
        }
    }

    /// <summary>Handles a chat line relayed through the server. Returns true if it was an Inferno command.</summary>
    public bool HandleChat(long senderUid, string text)
    {
        var outcome = CommandParser.Parse(text, out var command, out var error);
        if (outcome == ParseOutcome.NotACommand)
        {
            return false;
        }

        if (!_chatCopies.IsFirst(senderUid, text.Trim(), Time.realtimeSinceStartup))
        {
            return true;
        }

        var peer = Players.FindPeer(senderUid);
        if (peer is null)
        {
            log.LogDebug($"Ignoring chat command from unknown peer {senderUid}.");
            return true;
        }

        var reply = Run("chat", Players.FromPeer(peer), text, outcome, command, error, out _);
        Players.ShowMessage(peer, reply);
        return true;
    }

    /// <summary>Handles an F5 console command forwarded by a client. Returns true if it was an Inferno command.</summary>
    public bool HandleConsole(ZRpc rpc, string text)
    {
        var outcome = CommandParser.Parse(text, out var command, out var error);
        if (outcome == ParseOutcome.NotACommand)
        {
            return false;
        }

        var peer = Players.FindPeer(rpc);
        if (peer is null)
        {
            log.LogDebug("Ignoring console command from unknown connection.");
            return true;
        }

        var reply = Run("console", Players.FromPeer(peer), text, outcome, command, error, out _);
        Players.ConsolePrint(rpc, reply);
        return true;
    }

    /// <summary>
    /// Handles command text found on a sign. Returns true if it was an Inferno command; <paramref name="answer"/> is
    /// then the short answer to write back onto the sign.
    /// </summary>
    public bool HandleSign(ZDO sign, string text, out string answer)
    {
        answer = string.Empty;
        var outcome = CommandParser.Parse(text, out var command, out var error);
        if (outcome == ParseOutcome.NotACommand)
        {
            return false;
        }

        // The writer claims ownership of the sign before writing, so the owner is the author.
        var peer = Players.FindPeer(sign.GetOwner());
        CommandSender sender;
        if (peer is not null)
        {
            sender = Players.FromPeer(peer);
        }
        else
        {
            // Author left before the scan: fall back to the author id stored on the sign by the game.
            var author = sign.GetString(ZDOVars.s_author, "unknown");
            var name = sign.GetString(ZDOVars.s_authorDisplayName, author);
            sender = new CommandSender(name, author, ZNet.instance.IsAdmin(author));
        }

        var reply = Run("sign", sender, text, outcome, command, error, out _);
        if (peer is not null)
        {
            Players.ShowMessage(peer, reply);
        }

        answer = SignReply.Summarize(reply);
        return true;
    }

    /// <summary>Logs changes made by editing the config file directly.</summary>
    public void OnConfigReloaded()
    {
        var current = store.Snapshot();
        var changes = SettingsDiff.Compare(_snapshot, current);
        foreach (var change in changes)
        {
            log.LogInfo($"Config file edited: {change}");
        }

        _snapshot = current;
        if (changes.Count > 0)
        {
            SettingsChanged?.Invoke();
        }
    }

    private IReadOnlyList<string> Run(string source, CommandSender sender, string text, ParseOutcome outcome, ParsedCommand command, string error, out bool denied)
    {
        denied = false;
        switch (_limiter.TryTake(sender.PlatformId, Time.realtimeSinceStartup))
        {
            case RateDecision.DenySilently:
                return [];
            case RateDecision.DenyAndWarn:
                denied = true;
                log.LogWarning($"{source} commands from {sender} are coming too fast; ignoring them for a few seconds.");
                return ["Slow down: too many Inferno commands. Try again in a few seconds."];
        }

        log.LogInfo($"{source} command from {sender}: {Shorten(text)}");
        if (outcome == ParseOutcome.Invalid)
        {
            log.LogInfo($"  rejected: {error}");
            return [error];
        }

        CommandResult result;
        try
        {
            result = executor.Execute(command, sender);
        }
        catch (Exception e)
        {
            Diagnostics.Error($"running the {source} command '{Shorten(text)}'", e);
            return ["Something went wrong; see the server log."];
        }

        denied = result.Denied;
        if (result.Denied)
        {
            log.LogWarning($"  denied: {sender} is not an admin and AdminOnly is on.");
        }

        foreach (var change in result.Changes)
        {
            log.LogInfo($"  changed by {sender.Name}: {change}");
        }

        if (result.Changes.Count > 0)
        {
            _snapshot = store.Snapshot();
            SettingsChanged?.Invoke();
        }

        // On-screen replies fade quickly; the log keeps them.
        foreach (var line in result.Reply)
        {
            log.LogInfo($"  reply: {line}");
        }

        return result.Reply;
    }

    // Never write unbounded player text into the log.
    private static string Shorten(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= CommandParser.MaxLength ? trimmed : trimmed.Substring(0, CommandParser.MaxLength) + "…";
    }
}
