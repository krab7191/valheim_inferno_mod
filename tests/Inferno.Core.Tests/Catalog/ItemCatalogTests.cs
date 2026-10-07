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
    [InlineData("Standing wood torch")]
    [InlineData("standing wood torch")]
    [InlineData("STANDINGWOODTORCH")]
    [InlineData("standing-wood torch!")]
    public void TryResolve_InGameName_IgnoresCaseSpacesAndPunctuation(string target)
    {
        Assert.True(TestData.Catalog().TryResolve(target, out var items));
        Assert.Equal("piece_groundtorch_wood", Assert.Single(items).PrefabName);
    }

    [Fact]
    public void TryResolve_SharedInGameName_ReturnsAllItemsWithIt()
    {
        var castle = new CatalogItem("CastleKit_groundtorch_unlit", "Standing wood torch", ItemKind.LightSource, Fuel);
        var catalog = new ItemCatalog([TestData.Torch, castle, TestData.Hearth]);

        Assert.True(catalog.TryResolve("standing wood torch", out var items));
        Assert.Equal(["CastleKit_groundtorch_unlit", "piece_groundtorch_wood"], items.Select(i => i.PrefabName));
        Assert.Equal(["Standing wood torch"], catalog.Suggest("standing wood torchx"));
    }

    [Theory]
    [InlineData("torches", new[] { "piece_groundtorch", "piece_groundtorch_wood" })]
    [InlineData("torch", new[] { "piece_groundtorch", "piece_groundtorch_wood" })]
    [InlineData("TORCH", new[] { "piece_groundtorch", "piece_groundtorch_wood" })]
    [InlineData("hearths", new[] { "hearth" })]
    [InlineData("iron", new[] { "piece_groundtorch" })]
    public void TryResolve_WordGroups(string target, string[] expected)
    {
        var catalog = new ItemCatalog([TestData.Torch, TestData.IronTorch, TestData.Hearth, TestData.Smelter]);

        Assert.True(catalog.TryResolve(target, out var items));
        Assert.Equal(expected, items.Select(i => i.PrefabName));
    }

    [Theory]
    [InlineData("to")]   // too short to be a word group
    [InlineData("tos")]  // "to" after removing the plural s: too short
    [InlineData("xyzes")]
    public void TryResolve_ShortOrUnknownWords_DoNotMatch(string target) =>
        Assert.False(new ItemCatalog([TestData.Torch, TestData.IronTorch]).TryResolve(target, out _));

    [Theory]
    [InlineData("standing wood torchx", new[] { "Standing wood torch" })]
    [InlineData("standing wood torch extra", new[] { "Standing wood torch" })] // typed name contains a real one
    [InlineData("t", new[] { "Hearth", "Smelter", "Standing wood torch" })]
    [InlineData("zzz", new string[0])]
    [InlineData("!!", new string[0])]
    [InlineData(null, new string[0])]
    public void Suggest_FindsSimilarNames(string? target, string[] expected) =>
        Assert.Equal(expected, TestData.Catalog().Suggest(target!));

    [Fact]
    public void Suggest_RespectsMax() =>
        Assert.Equal(["Hearth"], TestData.Catalog().Suggest("t", max: 1));

    [Theory]
    [InlineData("Hot Tub!", "hottub")]
    [InlineData("Ölofen 2", "ölofen2")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_KeepsOnlyLettersAndDigits(string? text, string expected) =>
        Assert.Equal(expected, ItemCatalog.Normalize(text));

    [Theory]
    [InlineData("nope")]
    [InlineData("!!")]
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
