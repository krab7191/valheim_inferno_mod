using System;
using System.Linq;
using Inferno.Core.Catalog;
using Inferno.Core.Fuel;
using Inferno.Core.Settings;
using Xunit;

namespace Inferno.Core.Tests.Catalog;

public class ItemCatalogTests
{
    private static readonly FuelItemInfo Fuel = new(10f, true);

    [Fact]
    public void Items_AreSortedByPrefabName()
    {
        var catalog = TestData.Catalog();

        Assert.Equal(["hearth", "piece_groundtorch_wood", "smelter"], catalog.Items.Select(i => i.PrefabName));
    }

    [Theory]
    [InlineData("hearth")]
    [InlineData("HEARTH")]
    public void TryGet_IsCaseInsensitive(string name)
    {
        Assert.True(TestData.Catalog().TryGet(name, out var item));
        Assert.Equal("hearth", item.PrefabName);
    }

    [Theory]
    [InlineData("nope")]
    [InlineData(null)]
    public void TryGet_Unknown_ReturnsFalse(string? name)
    {
        Assert.False(TestData.Catalog().TryGet(name!, out var item));
        Assert.Null(item);
    }

    [Theory]
    [InlineData("all", new[] { "hearth", "piece_groundtorch_wood", "smelter" })]
    [InlineData("ALL", new[] { "hearth", "piece_groundtorch_wood", "smelter" })]
    [InlineData("lights", new[] { "hearth", "piece_groundtorch_wood" })]
    [InlineData("stations", new[] { "smelter" })]
    [InlineData("Smelter", new[] { "smelter" })]
    public void TryResolve_GroupsAndItems(string target, string[] expected)
    {
        Assert.True(TestData.Catalog().TryResolve(target, out var items));
        Assert.Equal(expected, items.Select(i => i.PrefabName));
    }

    [Theory]
    [InlineData("nope")]
    [InlineData(null)]
    public void TryResolve_Unknown_ReturnsFalse(string? target)
    {
        Assert.False(TestData.Catalog().TryResolve(target!, out var items));
        Assert.Empty(items);
    }

    [Fact]
    public void Constructor_NullItems_Throws() =>
        Assert.Throws<ArgumentNullException>("items", () => new ItemCatalog(null!));

    [Fact]
    public void Constructor_NullItem_Throws() =>
        Assert.Throws<ArgumentNullException>("items", () => new ItemCatalog([TestData.Torch, null!]));

    [Fact]
    public void Constructor_DuplicateName_Throws() =>
        Assert.Throws<ArgumentException>("items", () => new ItemCatalog([TestData.Torch, TestData.Torch]));

    [Theory]
    [InlineData("all")]
    [InlineData("Lights")]
    [InlineData("STATIONS")]
    public void Constructor_GroupName_Throws(string name) =>
        Assert.Throws<ArgumentException>("items", () => new ItemCatalog([new CatalogItem(name, "x", ItemKind.LightSource, Fuel)]));

    [Theory]
    [InlineData(null, "x", "prefabName")]
    [InlineData(" ", "x", "prefabName")]
    [InlineData("x", null, "displayName")]
    [InlineData("x", "", "displayName")]
    public void CatalogItem_BlankNames_Throw(string? prefab, string? display, string param) =>
        Assert.Throws<ArgumentException>(param, () => new CatalogItem(prefab!, display!, ItemKind.LightSource, Fuel));

    [Fact]
    public void CatalogItem_NullFuel_Throws() =>
        Assert.Throws<ArgumentNullException>("fuel", () => new CatalogItem("x", "x", ItemKind.LightSource, null!));

    [Fact]
    public void CatalogItem_StoresValues()
    {
        var item = TestData.Smelter;

        Assert.Equal("smelter", item.PrefabName);
        Assert.Equal("Smelter", item.DisplayName);
        Assert.Equal(ItemKind.FuelStation, item.Kind);
        Assert.Equal(20f, item.Fuel.MaxFuel);
        Assert.False(item.CanSchedule);
        Assert.True(TestData.Torch.CanSchedule);
    }
}
