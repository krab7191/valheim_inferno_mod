using System;
using System.Collections.Generic;
using Inferno.Core.Catalog;
using Inferno.Core.Time;

namespace Inferno.Core.Settings;

/// <summary>A named, one-command setup for a group of items (e.g. <c>!fires preset night</c>).</summary>
public sealed class Preset
{
    private readonly Func<CatalogItem, ItemSettings, ItemSettings?> _apply;

    internal Preset(string name, string defaultTarget, string description, Func<CatalogItem, ItemSettings, ItemSettings?> apply)
    {
        Name = name;
        DefaultTarget = defaultTarget;
        Description = description;
        _apply = apply;
    }

    /// <summary>Preset name as typed in commands.</summary>
    public string Name { get; }

    /// <summary>What the preset applies to when the player names no item.</summary>
    public string DefaultTarget { get; }

    /// <summary>One-line explanation for help texts.</summary>
    public string Description { get; }

    /// <summary>
    /// The settings an item gets from this preset, or null when the preset can't apply to it (e.g. a schedule
    /// for an item without an on/off switch). Smoke is left as it is.
    /// </summary>
    public ItemSettings? Apply(CatalogItem item, ItemSettings current) => _apply(item, current);
}

/// <summary>The built-in presets.</summary>
public static class Presets
{
    /// <summary>Always on, no schedule, vanilla burn rate (the default for light sources).</summary>
    public static Preset Eternal { get; } = new(
        "eternal",
        ItemCatalog.LightsGroup,
        "always on, never needs fuel (default for lights)",
        (_, s) => s.WithAlwaysOn(true).WithBurnRate(BurnRate.Vanilla).WithSchedule(DailySchedule.AlwaysOn));

    /// <summary>Lit from sunset to sunrise, never uses fuel.</summary>
    public static Preset Night { get; } = new(
        "night",
        ItemCatalog.LightsGroup,
        "lit 18:00-06:00, never uses fuel",
        (item, s) => item.CanSchedule
            ? s.WithAlwaysOn(false).WithBurnRate(BurnRate.Min).WithSchedule(DailySchedule.Night)
            : null);

    /// <summary>Plain game behaviour.</summary>
    public static Preset Vanilla { get; } = new(
        "vanilla",
        ItemCatalog.AllGroup,
        "plain game behaviour",
        (_, s) => s.WithAlwaysOn(false).WithBurnRate(BurnRate.Vanilla).WithSchedule(DailySchedule.AlwaysOn));

    /// <summary>All presets, in help order.</summary>
    public static IReadOnlyList<Preset> All { get; } = [Eternal, Night, Vanilla];

    /// <summary>Finds a preset by name (case-insensitive).</summary>
    public static bool TryGet(string? name, out Preset preset)
    {
        foreach (var candidate in All)
        {
            if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                preset = candidate;
                return true;
            }
        }

        preset = null!;
        return false;
    }
}
