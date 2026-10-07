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

        var tokens = (text ?? string.Empty).Split(Whitespace, StringSplitOptions.RemoveEmptyEntries);
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

        if (text!.Length > MaxLength)
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

            case "STATUS" when t.Length == 2:
                return new ParsedCommand(CommandKind.Status);

            case "LIST" when t.Length == 2:
                return new ParsedCommand(CommandKind.List, ItemCatalog.AllGroup);

            case "LIST" when t.Length == 3:
                return new ParsedCommand(CommandKind.List, t[2]);

            case "SHOW" when t.Length == 3:
                return new ParsedCommand(CommandKind.Show, t[2]);

            case "RESET" when t.Length == 3:
                return new ParsedCommand(CommandKind.Reset, t[2]);

            case "ALWAYSON" when t.Length == 4 && TryParseOnOff(t[3], out var on):
                return new ParsedCommand(CommandKind.AlwaysOn, t[2], flag: on);

            case "SMOKE" when t.Length == 4 && TryParseOnOff(t[3], out var smoke):
                return new ParsedCommand(CommandKind.Smoke, t[2], flag: smoke);

            case "BURNRATE" when t.Length == 4 && TryParseBurnRate(t[3], out var level):
                return new ParsedCommand(CommandKind.BurnRate, t[2], number: level);

            case "SCHEDULE" when t.Length == 4 && string.Equals(t[3], "off", StringComparison.OrdinalIgnoreCase):
                return new ParsedCommand(CommandKind.Schedule, t[2], schedule: DailySchedule.AlwaysOn);

            case "SCHEDULE" when t.Length == 5 && ClockSetting.TryParse(t[3], out var onTime) && ClockSetting.TryParse(t[4], out var offTime):
                return new ParsedCommand(CommandKind.Schedule, t[2], schedule: new DailySchedule(onTime, offTime));

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
        return null;
    }

    /// <summary>One-line usage for a sub-command, or a pointer to help for unknown ones.</summary>
    internal static string UsageFor(string subcommand, string prefix) => subcommand.ToUpperInvariant() switch
    {
        "HELP" => $"Usage: {prefix} help",
        "STATUS" => $"Usage: {prefix} status",
        "LIST" => $"Usage: {prefix} list [all|lights|stations]",
        "SHOW" => $"Usage: {prefix} show <item|all|lights|stations>",
        "RESET" => $"Usage: {prefix} reset <item|all|lights|stations>",
        "ALWAYSON" => $"Usage: {prefix} alwayson <item|group> on|off",
        "SMOKE" => $"Usage: {prefix} smoke <item|group> on|off  (seen only by players with the Inferno client mod)",
        "BURNRATE" => $"Usage: {prefix} burnrate <item|group> <{BurnRate.Min}..{BurnRate.Max}>  (0 = vanilla, each step 10 %)",
        "SCHEDULE" => $"Usage: {prefix} schedule <item|group> <on HH:MM> <off HH:MM>   or   {prefix} schedule <item|group> off",
        "ADMINONLY" => $"Usage: {prefix} adminonly on|off",
        "HIDECOMMANDS" => $"Usage: {prefix} hidecommands on|off",
        "IGNORERAIN" => $"Usage: {prefix} ignorerain on|off",
        "SERVEROWNERSHIP" => $"Usage: {prefix} serverownership on|off",
        _ => $"Unknown command '{subcommand}'. Type {prefix} help",
    };

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
