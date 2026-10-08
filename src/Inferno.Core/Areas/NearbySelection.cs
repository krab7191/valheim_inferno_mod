using System;
using System.Collections.Generic;

namespace Inferno.Core.Areas;

/// <summary>One fuel-burning object in the world, as found by a "nearby" command.</summary>
public sealed class NearbyObject
{
    /// <summary>Creates an entry.</summary>
    /// <param name="key">Stable id of the object (used to store its own settings).</param>
    /// <param name="prefabName">Its item type (prefab name).</param>
    /// <param name="allowed">Whether the player may change it (ward permission).</param>
    /// <exception cref="ArgumentNullException">A string is null.</exception>
    public NearbyObject(string key, string prefabName, bool allowed)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        PrefabName = prefabName ?? throw new ArgumentNullException(nameof(prefabName));
        Allowed = allowed;
    }

    /// <summary>Stable id of the object.</summary>
    public string Key { get; }

    /// <summary>Its item type (prefab name).</summary>
    public string PrefabName { get; }

    /// <summary>Whether the player may change it.</summary>
    public bool Allowed { get; }
}

/// <summary>The objects a "nearby" command applies to, and a description of the area for the reply.</summary>
public sealed class NearbySelection
{
    /// <summary>Creates a selection.</summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public NearbySelection(IReadOnlyList<NearbyObject> objects, string areaDescription)
    {
        Objects = objects ?? throw new ArgumentNullException(nameof(objects));
        AreaDescription = areaDescription ?? throw new ArgumentNullException(nameof(areaDescription));
    }

    /// <summary>Objects in the area.</summary>
    public IReadOnlyList<NearbyObject> Objects { get; }

    /// <summary>E.g. "in this ward's area" or "within 20 m".</summary>
    public string AreaDescription { get; }
}
