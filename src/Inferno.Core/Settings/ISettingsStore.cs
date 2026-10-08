namespace Inferno.Core.Settings;

/// <summary>Where settings are read from and written to (the BepInEx config file and the world data in the game).</summary>
public interface ISettingsStore
{
    /// <summary>Server-wide settings.</summary>
    GeneralSettings General { get; set; }

    /// <summary>Returns the settings of a catalog item (item type).</summary>
    ItemSettings GetItem(string prefabName);

    /// <summary>Stores the settings of a catalog item (item type).</summary>
    void SetItem(string prefabName, ItemSettings settings);

    /// <summary>
    /// Returns the own settings of one object in the world (set with a "nearby" command), or null when it simply
    /// follows its item type's settings.
    /// </summary>
    ItemSettings? GetObject(string objectKey);

    /// <summary>Stores one object's own settings; null removes them so it follows its item type again.</summary>
    void SetObject(string objectKey, ItemSettings? settings);
}
