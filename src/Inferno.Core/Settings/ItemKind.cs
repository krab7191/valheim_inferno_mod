namespace Inferno.Core.Settings;

/// <summary>What kind of fuel-burning item a prefab is. Decides its default settings.</summary>
public enum ItemKind
{
    /// <summary>Every fireplace-type item: torches, sconces, fires, hearths, braziers, lanterns, hot tubs, … Default: always on.</summary>
    LightSource,

    /// <summary>Smelters, ovens and other fuel users without an on/off switch. Default: vanilla.</summary>
    FuelStation,
}
