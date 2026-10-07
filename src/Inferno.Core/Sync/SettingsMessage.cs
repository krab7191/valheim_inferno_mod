using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Inferno.Core.Settings;
using Inferno.Core.Time;

namespace Inferno.Core.Sync;

/// <summary>
/// The server's current settings as sent to players who have the Inferno client mod (for the settings menu and
/// the client-side features). Vanilla clients never receive it.
/// </summary>
public sealed class SettingsMessage
{
    /// <summary>Wire format version. Clients ignore messages with another version.</summary>
    public const int ProtocolVersion = 1;

    private const string Header = "inferno-settings";
    private const char Field = '\t';
    private const char Line = '\n';

    /// <summary>Creates a message.</summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public SettingsMessage(bool canEdit, GeneralSettings general, IReadOnlyDictionary<string, ItemSettings> items)
    {
        CanEdit = canEdit;
        General = general ?? throw new ArgumentNullException(nameof(general));
        Items = items ?? throw new ArgumentNullException(nameof(items));
    }

    /// <summary>Whether the receiving player may change settings (see <c>PermissionPolicy</c>).</summary>
    public bool CanEdit { get; }

    /// <summary>Server-wide settings.</summary>
    public GeneralSettings General { get; }

    /// <summary>Item settings by prefab name.</summary>
    public IReadOnlyDictionary<string, ItemSettings> Items { get; }

    /// <summary>Encodes the message as text (sent as one RPC string parameter).</summary>
    /// <exception cref="ArgumentException">A prefab name contains a tab or newline.</exception>
    public string Encode()
    {
        var sb = new StringBuilder();
        Append(sb, Header, Number(ProtocolVersion));
        Append(sb, "canedit", Bit(CanEdit));
        Append(sb, "general", Bit(General.AdminOnly), Bit(General.HideCommands), Bit(General.IgnoreRain), Bit(General.ServerOwnership));
        foreach (var item in Items)
        {
            if (item.Key.IndexOf(Field) >= 0 || item.Key.IndexOf(Line) >= 0)
            {
                throw new ArgumentException($"Prefab name '{item.Key}' contains a tab or newline.", nameof(Items));
            }

            var s = item.Value;
            Append(
                sb,
                "item",
                item.Key,
                Bit(s.AlwaysOn),
                Number(s.BurnRateLevel),
                Number(s.Schedule.OnTime.MinutesSinceMidnight),
                Number(s.Schedule.OffTime.MinutesSinceMidnight),
                Bit(s.Smoke));
        }

        return sb.ToString();
    }

    /// <summary>Decodes a message. Returns false for anything malformed or of another protocol version.</summary>
    public static bool TryDecode(string? text, out SettingsMessage message)
    {
        message = null!;
        if (text is null)
        {
            return false;
        }

        var lines = text.Split(new[] { Line }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 3
            || !TryFields(lines[0], Header, 1, out var header) || !TryInt(header[0], out var version) || version != ProtocolVersion
            || !TryFields(lines[1], "canedit", 1, out var canEditFields) || !TryBit(canEditFields[0], out var canEdit)
            || !TryFields(lines[2], "general", 4, out var g)
            || !TryBit(g[0], out var adminOnly) || !TryBit(g[1], out var hide) || !TryBit(g[2], out var rain) || !TryBit(g[3], out var owned))
        {
            return false;
        }

        var items = new Dictionary<string, ItemSettings>(StringComparer.OrdinalIgnoreCase);
        for (var i = 3; i < lines.Length; i++)
        {
            if (!TryItem(lines[i], out var name, out var settings) || items.ContainsKey(name))
            {
                return false;
            }

            items.Add(name, settings);
        }

        message = new SettingsMessage(canEdit, new GeneralSettings(adminOnly, hide, rain, owned), items);
        return true;
    }

    private static bool TryItem(string line, out string name, out ItemSettings settings)
    {
        name = string.Empty;
        settings = null!;
        if (!TryFields(line, "item", 6, out var f)
            || f[0].Length == 0
            || !TryBit(f[1], out var alwaysOn)
            || !TryInt(f[2], out var burnRate) || burnRate is < BurnRate.Min or > BurnRate.Max
            || !TryMinutes(f[3], out var on) || !TryMinutes(f[4], out var off)
            || !TryBit(f[5], out var smoke))
        {
            return false;
        }

        name = f[0];
        settings = new ItemSettings(alwaysOn, burnRate, new DailySchedule(on, off), smoke);
        return true;
    }

    private static bool TryFields(string line, string tag, int count, out string[] fields)
    {
        var parts = line.Split(Field);
        fields = new string[count];
        if (parts.Length != count + 1 || parts[0] != tag)
        {
            return false;
        }

        Array.Copy(parts, 1, fields, 0, count);
        return true;
    }

    private static bool TryMinutes(string text, out TimeOfDay time)
    {
        time = default;
        if (!TryInt(text, out var minutes) || minutes is < 0 or >= TimeOfDay.MinutesPerDay)
        {
            return false;
        }

        time = TimeOfDay.FromHourMinute(minutes / 60, minutes % 60);
        return true;
    }

    private static bool TryBit(string text, out bool value)
    {
        value = text == "1";
        return value || text == "0";
    }

    private static bool TryInt(string text, out int value) =>
        int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);

    private static string Bit(bool value) => value ? "1" : "0";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static void Append(StringBuilder sb, params string[] fields)
    {
        sb.Append(string.Join(Field.ToString(), fields));
        sb.Append(Line);
    }
}
