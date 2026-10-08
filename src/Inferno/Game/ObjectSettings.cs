using System.Globalization;
using Inferno.Core.Settings;
using Inferno.Core.Time;

namespace Inferno.Game;

/// <summary>
/// A fire's own settings (set with a "nearby" command), stored in the fire's world data. They survive restarts,
/// stay with the fire, and vanilla ignores the extra keys, so removing Inferno leaves nothing behind that matters.
/// </summary>
internal static class ObjectSettings
{
    private static readonly int OwnKey = "Inferno_Own".GetStableHashCode();
    private static readonly int AlwaysOnKey = "Inferno_AlwaysOn".GetStableHashCode();
    private static readonly int BurnRateKey = "Inferno_BurnRate".GetStableHashCode();
    private static readonly int OnMinutesKey = "Inferno_OnMinutes".GetStableHashCode();
    private static readonly int OffMinutesKey = "Inferno_OffMinutes".GetStableHashCode();
    private static readonly int SmokeKey = "Inferno_Smoke".GetStableHashCode();

    // The owning client only accepts server data with a higher revision than its own copy (see FuelScanner).
    // Without this, its next update could carry its old copy back and the fire's own settings would be lost.
    private const uint RevisionLead = 8;

    /// <summary>Text form of an object id, used as the key in commands and the undo history.</summary>
    public static string KeyOf(ZDOID id) => id.UserID.ToString(CultureInfo.InvariantCulture) + ":" + id.ID.ToString(CultureInfo.InvariantCulture);

    public static bool TryParseKey(string key, out ZDOID id)
    {
        id = ZDOID.None;
        var parts = key.Split(':');
        if (parts.Length != 2
            || !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var user)
            || !uint.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        id = new ZDOID(user, number);
        return true;
    }

    /// <summary>The fire's own settings, or null when it follows its item type.</summary>
    public static ItemSettings? Read(ZDO zdo)
    {
        if (zdo.GetInt(OwnKey) != 1)
        {
            return null;
        }

        var burnRate = zdo.GetInt(BurnRateKey);
        if (burnRate is < BurnRate.Min or > BurnRate.Max)
        {
            burnRate = BurnRate.Vanilla;
        }

        return new ItemSettings(
            zdo.GetInt(AlwaysOnKey) == 1,
            burnRate,
            new DailySchedule(Minutes(zdo.GetInt(OnMinutesKey)), Minutes(zdo.GetInt(OffMinutesKey))),
            zdo.GetInt(SmokeKey, 1) == 1);
    }

    /// <summary>Stores the fire's own settings; null makes it follow its item type again.</summary>
    public static void Write(ZDO zdo, ItemSettings? settings)
    {
        if (settings is null)
        {
            zdo.Set(OwnKey, 0);
        }
        else
        {
            zdo.Set(AlwaysOnKey, settings.AlwaysOn ? 1 : 0);
            zdo.Set(BurnRateKey, settings.BurnRateLevel);
            zdo.Set(OnMinutesKey, settings.Schedule.OnTime.MinutesSinceMidnight);
            zdo.Set(OffMinutesKey, settings.Schedule.OffTime.MinutesSinceMidnight);
            zdo.Set(SmokeKey, settings.Smoke ? 1 : 0);
            zdo.Set(OwnKey, 1);
        }

        zdo.DataRevision += RevisionLead;
    }

    private static TimeOfDay Minutes(int minutes) =>
        minutes is >= 0 and < TimeOfDay.MinutesPerDay ? TimeOfDay.FromHourMinute(minutes / 60, minutes % 60) : default;
}
