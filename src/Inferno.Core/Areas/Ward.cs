using System;
using System.Collections.Generic;

namespace Inferno.Core.Areas;

/// <summary>A ward (guard stone) as the server sees it in the world data.</summary>
public sealed class Ward
{
    /// <summary>Creates ward info.</summary>
    /// <param name="x">World X of the ward.</param>
    /// <param name="z">World Z of the ward.</param>
    /// <param name="radius">Protection radius (<c>PrivateArea.m_radius</c>); must be ≥ 0.</param>
    /// <param name="enabled">Whether the ward is switched on.</param>
    /// <param name="creator">Player ID of the player who built it.</param>
    /// <param name="permitted">Player IDs the creator added to the ward.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> is negative or not a number.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="permitted"/> is null.</exception>
    public Ward(float x, float z, float radius, bool enabled, long creator, IReadOnlyCollection<long> permitted)
    {
        if (!(radius >= 0f) || float.IsInfinity(radius))
        {
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "Radius must be a finite number ≥ 0.");
        }

        X = x;
        Z = z;
        Radius = radius;
        Enabled = enabled;
        Creator = creator;
        Permitted = permitted ?? throw new ArgumentNullException(nameof(permitted));
    }

    /// <summary>World X.</summary>
    public float X { get; }

    /// <summary>World Z.</summary>
    public float Z { get; }

    /// <summary>Protection radius.</summary>
    public float Radius { get; }

    /// <summary>Whether the ward is switched on.</summary>
    public bool Enabled { get; }

    /// <summary>Player ID of the builder.</summary>
    public long Creator { get; }

    /// <summary>Player IDs with access.</summary>
    public IReadOnlyCollection<long> Permitted { get; }

    /// <summary>Port of <c>PrivateArea.IsInside</c>: horizontal distance only (height is ignored).</summary>
    public bool Covers(float x, float z)
    {
        var dx = X - x;
        var dz = Z - z;
        return Math.Sqrt((dx * dx) + (dz * dz)) < Radius;
    }

    /// <summary>Whether the player built this ward or was added to it.</summary>
    public bool Grants(long playerId)
    {
        if (Creator == playerId)
        {
            return true;
        }

        foreach (var id in Permitted)
        {
            if (id == playerId)
            {
                return true;
            }
        }

        return false;
    }
}
