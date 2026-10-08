using System;
using Inferno.Core.Areas;
using Xunit;

namespace Inferno.Core.Tests.Areas;

public class WardTests
{
    private static Ward W(float x = 0, float z = 0, float radius = 32, bool enabled = true, long creator = 1, params long[] permitted) =>
        new(x, z, radius, enabled, creator, permitted);

    [Theory]
    [InlineData(0f, 0f, true)]
    [InlineData(31.9f, 0f, true)]
    [InlineData(32f, 0f, false)]   // edge excluded, like PrivateArea.IsInside
    [InlineData(20f, 20f, true)]
    [InlineData(30f, 30f, false)]
    public void Covers_UsesHorizontalDistance(float x, float z, bool expected) =>
        Assert.Equal(expected, W().Covers(x, z));

    [Fact]
    public void Grants_CreatorAndPermitted()
    {
        var ward = W(creator: 1, permitted: [5, 7]);

        Assert.True(ward.Grants(1));
        Assert.True(ward.Grants(7));
        Assert.False(ward.Grants(9));
    }

    [Fact]
    public void Constructor_Validates()
    {
        Assert.Throws<ArgumentOutOfRangeException>("radius", () => W(radius: -1));
        Assert.Throws<ArgumentOutOfRangeException>("radius", () => W(radius: float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>("radius", () => W(radius: float.PositiveInfinity));
        Assert.Throws<ArgumentNullException>("permitted", () => new Ward(0, 0, 1, true, 1, null!));

        var ward = W(x: 3, z: 4, radius: 5, enabled: false, creator: 2, permitted: [8]);
        Assert.Equal(3f, ward.X);
        Assert.Equal(4f, ward.Z);
        Assert.Equal(5f, ward.Radius);
        Assert.False(ward.Enabled);
        Assert.Equal(2L, ward.Creator);
        Assert.Equal([8L], ward.Permitted);
    }
}

public class NearbyAreaTests
{
    private static Ward W(float x, float z, bool enabled = true, long creator = 1, params long[] permitted) =>
        new(x, z, 32, enabled, creator, permitted);

    [Fact]
    public void InsideWard_AreaIsTheWard()
    {
        var area = NearbyArea.Around([W(0, 0), W(500, 500)], 5, 5);

        Assert.True(area.IsWardArea);
        Assert.Equal("in this ward's area", area.Description);
        Assert.True(area.Contains(-30, 0));   // far side of the ward, beyond 20 m
        Assert.False(area.Contains(40, 0));
        Assert.False(area.Contains(500, 500)); // another ward elsewhere
    }

    [Fact]
    public void OverlappingWards_AreaIsBoth()
    {
        var area = NearbyArea.Around([W(0, 0), W(40, 0)], 20, 0);

        Assert.Equal("in the area of 2 wards", area.Description);
        Assert.True(area.Contains(-25, 0));
        Assert.True(area.Contains(65, 0));
    }

    [Fact]
    public void NoWard_FallsBackToRadius()
    {
        var area = NearbyArea.Around([W(0, 0, enabled: false), W(500, 500)], 0, 0);

        Assert.False(area.IsWardArea);
        Assert.Equal("within 20 m", area.Description);
        Assert.True(area.Contains(20, 0));
        Assert.False(area.Contains(15, 15));
        Assert.Equal("within 10 m", NearbyArea.Around([], 0, 0, 10).Description);
    }

    [Fact]
    public void Around_Validates()
    {
        Assert.Throws<ArgumentNullException>("wards", () => NearbyArea.Around(null!, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>("fallbackRadius", () => NearbyArea.Around([], 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>("fallbackRadius", () => NearbyArea.Around([], 0, 0, float.PositiveInfinity));
        Assert.Throws<ArgumentNullException>("wards", () => NearbyArea.CanAccess(null!, 0, 0, 1));
    }

    [Theory]
    [InlineData(1L, true)]    // creator
    [InlineData(5L, true)]    // permitted
    [InlineData(9L, false)]   // stranger
    public void CanAccess_InsideWard(long player, bool expected) =>
        Assert.Equal(expected, NearbyArea.CanAccess([W(0, 0, creator: 1, permitted: 5)], 0, 0, player));

    [Fact]
    public void CanAccess_NoWardOrDisabledWard_IsOpen()
    {
        Assert.True(NearbyArea.CanAccess([], 0, 0, 9));
        Assert.True(NearbyArea.CanAccess([W(0, 0, enabled: false)], 0, 0, 9));
        Assert.True(NearbyArea.CanAccess([W(100, 100)], 0, 0, 9));
    }

    [Fact]
    public void CanAccess_AnyCoveringWardGrants_LikeVanilla() =>
        Assert.True(NearbyArea.CanAccess([W(0, 0, creator: 1), W(10, 0, creator: 9)], 5, 0, 9));
}

public class NearbySelectionTests
{
    [Fact]
    public void Constructors_Validate()
    {
        Assert.Throws<ArgumentNullException>("key", () => new NearbyObject(null!, "x", true));
        Assert.Throws<ArgumentNullException>("prefabName", () => new NearbyObject("k", null!, true));
        Assert.Throws<ArgumentNullException>("objects", () => new NearbySelection(null!, "x"));
        Assert.Throws<ArgumentNullException>("areaDescription", () => new NearbySelection([], null!));

        var obj = new NearbyObject("k", "hearth", false);
        Assert.Equal("k", obj.Key);
        Assert.Equal("hearth", obj.PrefabName);
        Assert.False(obj.Allowed);
    }
}
