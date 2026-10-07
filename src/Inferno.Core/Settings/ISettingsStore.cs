namespace Inferno.Core.Settings;

/// <summary>Where settings are read from and written to (the BepInEx config file in the game).</summary>
public interface ISettingsStore
{
    /// <summary>Server-wide settings.</summary>
    GeneralSettings General { get; set; }

    /// <summary>Returns the settings of a catalog item.</summary>
    ItemSettings GetItem(string prefabName);

    /// <summary>Stores the settings of a catalog item.</summary>
    void SetItem(string prefabName, ItemSettings settings);
}
