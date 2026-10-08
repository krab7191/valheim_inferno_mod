using System;
using System.Linq;
using Inferno.Core.Catalog;
using Inferno.Core.Settings;
using Inferno.Core.Time;

namespace Inferno.Core.Commands;

/// <summary>Result of parsing a chat line.</summary>
public enum ParseOutcome
{
    /// <summary>The line is ordinary chat, not a <c>!fires</c> command.</summary>
    NotACommand,

    /// <summary>A valid command.</summary>
    Command,

    /// <summary>A <c>!fires</c> command with a syntax error; see the error message.</summary>
    Invalid,
}

/// <summary>
/// Parses Inferno commands from chat, signs (<c>!fires …</c>) and the F5 console (<c>listkeys fires …</c>).
/// </summary>
/// <remarks>
/// Chat and signs use plain text because a vanilla client runs anything starting with <c>/</c> locally. The F5
/// console only forwards built-in server commands, so console commands ride on the read-only vanilla
/// <c>listkeys</c> command (harmless if Inferno is not installed).
/// </remarks>
public static class CommandParser
{
    /// <summary>Prefix that marks a chat line or sign text as an Inferno command.</summary>
    public const string Prefix = "!fires";

    /// <summary>Prefix of Inferno commands typed in the F5 console.</summary>
    public const string ConsolePrefix = "listkeys fires";

    /// <summary>Longest accepted command text. Real commands are far shorter; this bounds parsing and logging.</summary>
    public const int MaxLength = 200;

    private const string ConsoleCarrier = "listkeys";
    private const string ConsoleKeyword = "fires";

    private static readonly char[] Whitespace = [' ', '\t'];

    /// <summary>Parses one chat line.</summary>
    /// <param name="text">The chat text.</param>
    /// <param name="command">The command, when the outcome is <see cref="ParseOutcome.Command"/>.</param>
    /// <param name="error">A message for the player, when the outcome is <see cref="ParseOutcome.Invalid"/>.</param>
    public static ParseOutcome Parse(string? text, out ParsedCommand command, out string error)
    {
        command = null!;
        error = string.Empty;

        // Formatting such as "<color=green>!fires status" (signs, chat) is not part of the command.
        text = RichText.Strip(text);
        var tokens = text.Split(Whitespace, StringSplitOptions.RemoveEmptyEntries);
        string prefix;
        if (tokens.Length >= 1 && string.Equals(tokens[0], Prefix, StringComparison.OrdinalIgnoreCase))
        {
            prefix = Prefix;
        }
        else if (tokens.Length >= 2
            && string.Equals(tokens[0], ConsoleCarrier, StringComparison.OrdinalIgnoreCase)
            && string.Equals(tokens[1], ConsoleKeyword, StringComparison.OrdinalIgnoreCase))
        {
            prefix = ConsolePrefix;
            tokens = tokens.Skip(1).ToArray();
        }
        else
        {
            return ParseOutcome.NotACommand;
        }

        if (text.Length > MaxLength)
        {
            error = $"Command too long (max {MaxLength} characters).";
            return ParseOutcome.Invalid;
        }

        if (tokens.Length == 1)
        {
            command = new ParsedCommand(CommandKind.Help);
            return ParseOutcome.Command;
        }

        var parsed = ParseSubcommand(tokens, prefix, out error);
        if (parsed is null)
        {
            return ParseOutcome.Invalid;
        }

        command = parsed;
        return ParseOutcome.Command;
    }

    private static ParsedCommand? ParseSubcommand(string[] t, string prefix, out string error)
    {
        error = string.Empty;
        switch (t[1].ToUpperInvariant())
        {
            case "HELP" when t.Length == 2:
                return new ParsedCommand(CommandKind.Help);

            case "HELP" when t.Length == 3:
                return new ParsedCommand(CommandKind.Help, name: t[2]);

            case "STATUS" when t.Length == 2:
                return new ParsedCommand(CommandKind.Status);

            case "LIST" when t.Length == 2:
                return new ParsedCommand(CommandKind.List, ItemCatalog.AllGroup);

            // Item names may contain spaces ("hot tub"): the item is every word between the command word and the
            // command's own trailing arguments.
            case "LIST" when t.Length >= 3:
                return new ParsedCommand(CommandKind.List, Item(t, 0));

            case "SHOW" when t.Length >= 3:
                return new ParsedCommand(CommandKind.Show, Item(t, 0));

            case "RESET" when t.Length >= 3:
                return new ParsedCommand(CommandKind.Reset, Item(t, 0));

            case "ALWAYSON" when t.Length >= 4 && TryParseOnOff(t[t.Length - 1], out var on):
                return new ParsedCommand(CommandKind.AlwaysOn, Item(t, 1), flag: on);

            case "SMOKE" when t.Length >= 4 && TryParseOnOff(t[t.Length - 1], out var smoke):
                return new ParsedCommand(CommandKind.Smoke, Item(t, 1), flag: smoke);

            case "BURNRATE" when t.Length >= 4 && TryParseBurnRate(t[t.Length - 1], out var level):
                return new ParsedCommand(CommandKind.BurnRate, Item(t, 1), number: level);

            case "SCHEDULE" when t.Length >= 4 && TryParseScheduleWord(t[t.Length - 1], out var named):
                return new ParsedCommand(CommandKind.Schedule, Item(t, 1), schedule: named);

            case "SCHEDULE" when t.Length >= 5
                && ClockSetting.TryParse(t[t.Length - 2], out var onTime)
                && ClockSetting.TryParse(t[t.Length - 1], out var offTime):
                return new ParsedCommand(CommandKind.Schedule, Item(t, 2), schedule: new DailySchedule(onTime, offTime));

            case "PRESET" when t.Length >= 3:
                return new ParsedCommand(CommandKind.Preset, t.Length > 3 ? Item(t, 0, first: 3) : null, name: t[2]);

            case "UNDO" when t.Length == 2:
                return new ParsedCommand(CommandKind.Undo);

            case "ADMINONLY" when t.Length == 3 && TryParseOnOff(t[2], out var adminOnly):
                return new ParsedCommand(CommandKind.AdminOnly, flag: adminOnly);

            case "HIDECOMMANDS" when t.Length == 3 && TryParseOnOff(t[2], out var hide):
                return new ParsedCommand(CommandKind.HideCommands, flag: hide);

            case "IGNORERAIN" when t.Length == 3 && TryParseOnOff(t[2], out var ignoreRain):
                return new ParsedCommand(CommandKind.IgnoreRain, flag: ignoreRain);

            case "SERVEROWNERSHIP" when t.Length == 3 && TryParseOnOff(t[2], out var serverOwnership):
                return new ParsedCommand(CommandKind.ServerOwnership, flag: serverOwnership);
        }

        error = UsageFor(t[1], prefix);
        if (!IsSubcommand(t[1]) && SuggestOrder(t, prefix) is { } suggestion)
        {
            error = $"Did you mean: {suggestion}";
        }

        return null;
    }

    private static readonly string[] Subcommands =
    [
        "HELP", "STATUS", "LIST", "SHOW", "RESET", "ALWAYSON", "SMOKE", "BURNRATE", "SCHEDULE", "PRESET", "UNDO",
        "ADMINONLY", "HIDECOMMANDS", "IGNORERAIN", "SERVEROWNERSHIP",
    ];

    private static bool IsSubcommand(string word) => Array.IndexOf(Subcommands, word.ToUpperInvariant()) >= 0;

    // "!fires all preset eternal" → "!fires preset eternal all"; "!fires torches burnrate 5" → "!fires burnrate
    // torches 5". The command word moves to the front: for presets the name follows it, for everything else the
    // words before it are the item. Only suggested if the result is a valid command; never run automatically.
    private static string? SuggestOrder(string[] t, string prefix)
    {
        for (var i = 2; i < t.Length; i++)
        {
            if (!IsSubcommand(t[i]))
            {
                continue;
            }

            var before = t.Skip(1).Take(i - 1).ToList();
            var after = t.Skip(i + 1).ToList();
            var reordered = string.Equals(t[i], "preset", StringComparison.OrdinalIgnoreCase) && after.Count > 0
                ? new[] { t[i], after[0] }.Concat(before).Concat(after.Skip(1))
                : new[] { t[i] }.Concat(before).Concat(after);
            var candidate = prefix + " " + string.Join(" ", reordered);
            return Parse(candidate, out _, out _) == ParseOutcome.Command ? candidate : null;
        }

        return null;
    }

    /// <summary>One-line usage for a sub-command, or a pointer to help for unknown ones.</summary>
    internal static string UsageFor(string subcommand, string prefix) => subcommand.ToUpperInvariant() switch
    {
        "HELP" => $"Usage: {prefix} help [items|presets|admin]",
        "STATUS" => $"Usage: {prefix} status",
        "SHOW" => $"Usage: {prefix} show <item>   (item = name as in game, e.g. hot tub; or all, lights, stations)",
        "RESET" => $"Usage: {prefix} reset <item>",
        "ALWAYSON" => $"Usage: {prefix} alwayson <item> on|off",
        "SMOKE" => $"Usage: {prefix} smoke <item> on|off  (seen only by players with the Inferno client mod)",
        "BURNRATE" => $"Usage: {prefix} burnrate <item> <{BurnRate.Min}..{BurnRate.Max}>  (0 = vanilla, each step 10 %)",
        "SCHEDULE" => $"Usage: {prefix} schedule <item> <on HH:MM> <off HH:MM>   or   {prefix} schedule <item> night|day|off",
        "PRESET" => $"Usage: {prefix} preset eternal|night|vanilla [item]",
        "UNDO" => $"Usage: {prefix} undo",
        "ADMINONLY" => $"Usage: {prefix} adminonly on|off",
        "HIDECOMMANDS" => $"Usage: {prefix} hidecommands on|off",
        "IGNORERAIN" => $"Usage: {prefix} ignorerain on|off",
        "SERVEROWNERSHIP" => $"Usage: {prefix} serverownership on|off",
        _ => $"Unknown command '{subcommand}'. Type {prefix} help",
    };

    // Joins the item words: everything after the command word (or after `first`), minus the trailing arguments.
    private static string Item(string[] tokens, int trailingArguments, int first = 2) =>
        string.Join(" ", tokens, first, tokens.Length - first - trailingArguments);

    // "off" = no schedule; "night" = sunset to sunrise; "day" = sunrise to sunset.
    private static bool TryParseScheduleWord(string text, out DailySchedule schedule)
    {
        switch (text.ToUpperInvariant())
        {
            case "OFF":
                schedule = DailySchedule.AlwaysOn;
                return true;
            case "NIGHT":
                schedule = DailySchedule.Night;
                return true;
            case "DAY":
                schedule = DailySchedule.Day;
                return true;
            default:
                schedule = default;
                return false;
        }
    }

    private static bool TryParseOnOff(string text, out bool value)
    {
        value = string.Equals(text, "on", StringComparison.OrdinalIgnoreCase);
        return value || string.Equals(text, "off", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseBurnRate(string text, out int level)
    {
        level = 0;
        var negative = text.StartsWith("-", StringComparison.Ordinal);
        var digits = negative || text.StartsWith("+", StringComparison.Ordinal) ? text.Substring(1) : text;
        if (digits.Length is < 1 or > 2 || !TimeOfDay.TryParseDigits(digits, out var magnitude) || magnitude > BurnRate.Max)
        {
            return false;
        }

        level = negative ? -magnitude : magnitude;
        return true;
    }
}
