using System;
using System.Collections.Generic;
using System.Linq;
using Inferno.Core.Settings;

namespace Inferno.Core.Catalog;

/// <summary>All fuel-burning items found in the game, and resolution of command targets.</summary>
public sealed class ItemCatalog
{
    /// <summary>Target that selects every item.</summary>
    public const string AllGroup = "all";

    /// <summary>Target that selects every light source.</summary>
    public const string LightsGroup = "lights";

    /// <summary>Target that selects every fuel station.</summary>
    public const string StationsGroup = "stations";

    private readonly Dictionary<string, CatalogItem> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CatalogItem> _items = [];

    /// <summary>Creates a catalog.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> or an item in it is null.</exception>
    /// <exception cref="ArgumentException">Two items share a prefab name, or a prefab is named like a group.</exception>
    public ItemCatalog(IEnumerable<CatalogItem> items)
    {
        if (items is null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        foreach (var item in items)
        {
            if (item is null)
            {
                throw new ArgumentNullException(nameof(items), "Catalog items must not be null.");
            }

            if (IsGroupName(item.PrefabName))
            {
                throw new ArgumentException($"Prefab name '{item.PrefabName}' is reserved for a group.", nameof(items));
            }

            if (_byName.ContainsKey(item.PrefabName))
            {
                throw new ArgumentException($"Duplicate prefab '{item.PrefabName}'.", nameof(items));
            }

            _byName.Add(item.PrefabName, item);
            _items.Add(item);
        }

        _items.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.PrefabName, b.PrefabName));
    }

    /// <summary>All items, sorted by prefab name.</summary>
    public IReadOnlyList<CatalogItem> Items => _items;

    /// <summary>Looks up an item by prefab name (case-insensitive).</summary>
    public bool TryGet(string prefabName, out CatalogItem item)
    {
        if (prefabName is not null && _byName.TryGetValue(prefabName, out var found))
        {
            item = found;
            return true;
        }

        item = null!;
        return false;
    }

    /// <summary>
    /// Resolves a command target: a group (<c>all</c>, <c>lights</c>, <c>stations</c>) or a prefab name.
    /// Returns false for unknown targets.
    /// </summary>
    public bool TryResolve(string target, out IReadOnlyList<CatalogItem> items)
    {
        switch (target?.ToLowerInvariant())
        {
            case AllGroup:
                items = _items;
                return true;
            case LightsGroup:
                items = _items.Where(i => i.Kind == ItemKind.LightSource).ToList();
                return true;
            case StationsGroup:
                items = _items.Where(i => i.Kind == ItemKind.FuelStation).ToList();
                return true;
        }

        if (TryGet(target!, out var item))
        {
            items = [item];
            return true;
        }

        items = [];
        return false;
    }

    private static bool IsGroupName(string name) =>
        string.Equals(name, AllGroup, StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, LightsGroup, StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, StationsGroup, StringComparison.OrdinalIgnoreCase);
}
