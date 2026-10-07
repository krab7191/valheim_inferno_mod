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

    /// <summary>Shortest word that can select a group of items by name.</summary>
    public const int MinKeywordLength = 3;

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
    /// Resolves a command target: a group (<c>all</c>, <c>lights</c>, <c>stations</c>), an internal prefab name, or an
    /// in-game name. In-game names ignore case, spaces and punctuation ("Hot Tub" = "hot tub" = "hottub"); several
    /// items can share one in-game name, and then all of them are returned. Returns false for unknown targets.
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

        var key = Normalize(target);
        var byName = key.Length == 0 ? [] : _items.Where(i => Normalize(i.DisplayName) == key).ToList();
        if (byName.Count > 0)
        {
            items = byName;
            return true;
        }

        items = MatchKeyword(key);
        return items.Count > 0;
    }

    /// <summary>
    /// Word groups: "torches", "torch", "braziers", "fires", … select every item whose in-game name contains the
    /// word (singular or plural). At least <see cref="MinKeywordLength"/> letters, so short fragments don't match
    /// half the catalog.
    /// </summary>
    private List<CatalogItem> MatchKeyword(string key)
    {
        foreach (var stem in Stems(key))
        {
            if (stem.Length < MinKeywordLength)
            {
                continue;
            }

            var matches = _items.Where(i => Normalize(i.DisplayName).Contains(stem)).ToList();
            if (matches.Count > 0)
            {
                return matches;
            }
        }

        return [];
    }

    // "torches" → torches, torche, torch; "fires" → fires, fire. Tried in that order.
    private static IEnumerable<string> Stems(string key)
    {
        yield return key;
        if (key.EndsWith("s", StringComparison.Ordinal))
        {
            yield return key.Substring(0, key.Length - 1);
        }

        if (key.EndsWith("es", StringComparison.Ordinal))
        {
            yield return key.Substring(0, key.Length - 2);
        }
    }

    /// <summary>
    /// In-game names that look like what the player typed (one contains the other, ignoring case, spaces and
    /// punctuation), for "did you mean" hints. At most <paramref name="max"/>, sorted.
    /// </summary>
    public IReadOnlyList<string> Suggest(string target, int max = 3)
    {
        var key = Normalize(target);
        if (key.Length == 0)
        {
            return [];
        }

        return _items
            .Select(i => i.DisplayName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(name =>
            {
                var candidate = Normalize(name);
                return candidate.Contains(key) || key.Contains(candidate);
            })
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToList();
    }

    /// <summary>Lower-case letters and digits only, so names match regardless of spacing and punctuation.</summary>
    internal static string Normalize(string? text)
    {
        if (text is null)
        {
            return string.Empty;
        }

        var chars = new char[text.Length];
        var count = 0;
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
            {
                chars[count++] = char.ToLowerInvariant(c);
            }
        }

        return new string(chars, 0, count);
    }

    private static bool IsGroupName(string name) =>
        string.Equals(name, AllGroup, StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, LightsGroup, StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, StationsGroup, StringComparison.OrdinalIgnoreCase);
}
