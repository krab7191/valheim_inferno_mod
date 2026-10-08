using System.Collections.Generic;
using Inferno.Core.Catalog;
using Inferno.Core.Fuel;
using Inferno.Core.Settings;

namespace Inferno.Core.Tests;

/// <summary>Shared test fixtures.</summary>
internal static class TestData
{
    public static CatalogItem Torch => new("piece_groundtorch_wood", "Standing wood torch", ItemKind.LightSource, new FuelItemInfo(4f, true));

    public static CatalogItem IronTorch => new("piece_groundtorch", "Standing iron torch", ItemKind.LightSource, new FuelItemInfo(6f, true));

    public static CatalogItem Hearth => new("hearth", "Hearth", ItemKind.LightSource, new FuelItemInfo(20f, true));

    public static CatalogItem Smelter => new("smelter", "Smelter", ItemKind.FuelStation, new FuelItemInfo(20f, false));

    public static ItemCatalog Catalog() => new([Torch, Hearth, Smelter]);
}

/// <summary>In-memory settings store that applies kind-based defaults like the real one.</summary>
internal sealed class FakeSettingsStore(ItemCatalog catalog) : ISettingsStore
{
    private readonly Dictionary<string, ItemSettings> _items = [];
    private readonly Dictionary<string, ItemSettings> _objects = [];

    public GeneralSettings General { get; set; } = GeneralSettings.Default;

    public int Writes { get; private set; }

    public ItemSettings GetItem(string prefabName)
    {
        if (_items.TryGetValue(prefabName, out var settings))
        {
            return settings;
        }

        catalog.TryGet(prefabName, out var item);
        return ItemSettings.DefaultFor(item.Kind);
    }

    public void SetItem(string prefabName, ItemSettings settings)
    {
        _items[prefabName] = settings;
        Writes++;
    }

    public ItemSettings? GetObject(string objectKey) => _objects.TryGetValue(objectKey, out var s) ? s : null;

    public void SetObject(string objectKey, ItemSettings? settings)
    {
        if (settings is null)
        {
            _objects.Remove(objectKey);
        }
        else
        {
            _objects[objectKey] = settings;
        }

        Writes++;
    }
}
