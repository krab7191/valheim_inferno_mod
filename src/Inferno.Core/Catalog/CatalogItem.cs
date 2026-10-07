using System;
using Inferno.Core.Fuel;
using Inferno.Core.Settings;

namespace Inferno.Core.Catalog;

/// <summary>One fuel-burning item type (prefab) found in the game.</summary>
public sealed class CatalogItem
{
    /// <summary>Creates a catalog item.</summary>
    /// <param name="prefabName">Internal prefab name, e.g. <c>piece_groundtorch_wood</c>.</param>
    /// <param name="displayName">Player-facing name (localized if available).</param>
    /// <param name="kind">Light source or fuel station.</param>
    /// <param name="fuel">Fuel facts from the prefab.</param>
    /// <exception cref="ArgumentException">A name is null or blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="fuel"/> is null.</exception>
    public CatalogItem(string prefabName, string displayName, ItemKind kind, FuelItemInfo fuel)
    {
        if (string.IsNullOrWhiteSpace(prefabName))
        {
            throw new ArgumentException("Prefab name is required.", nameof(prefabName));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        PrefabName = prefabName;
        DisplayName = displayName;
        Kind = kind;
        Fuel = fuel ?? throw new ArgumentNullException(nameof(fuel));
    }

    /// <summary>Internal prefab name; used as config section and command target.</summary>
    public string PrefabName { get; }

    /// <summary>Player-facing name.</summary>
    public string DisplayName { get; }

    /// <summary>Light source or fuel station.</summary>
    public ItemKind Kind { get; }

    /// <summary>Fuel facts from the prefab.</summary>
    public FuelItemInfo Fuel { get; }

    /// <summary>Whether the item can follow a schedule.</summary>
    public bool CanSchedule => Fuel.HasOnOffSwitch;
}
