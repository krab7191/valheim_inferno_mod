using System;
using System.Collections.Generic;
using System.Linq;

namespace Inferno.Core.Areas;

/// <summary>
/// What "nearby" means for a command: the area of the ward(s) the command was given in, or — where there is no
/// ward — a circle around that spot. Wards keep bases private; without a ward everything is shared, as in vanilla.
/// </summary>
public sealed class NearbyArea
{
    /// <summary>Radius used where no ward covers the spot.</summary>
    public const float DefaultRadius = 20f;

    private readonly IReadOnlyList<Ward> _wards;
    private readonly float _x;
    private readonly float _z;
    private readonly float _radius;

    private NearbyArea(IReadOnlyList<Ward> wards, float x, float z, float radius)
    {
        _wards = wards;
        _x = x;
        _z = z;
        _radius = radius;
    }

    /// <summary>True when the area is defined by wards (a base), false for the plain radius.</summary>
    public bool IsWardArea => _wards.Count > 0;

    /// <summary>Short description for replies: "in this ward's area", "in the area of 2 wards" or "within 20 m".</summary>
    public string Description => IsWardArea
        ? (_wards.Count == 1 ? "in this ward's area" : $"in the area of {_wards.Count} wards")
        : string.Format(System.Globalization.CultureInfo.InvariantCulture, "within {0:0} m", _radius);

    /// <summary>Works out the area around a spot (a sign, or the player for chat and console commands).</summary>
    /// <param name="wards">All wards in the world.</param>
    /// <param name="x">World X of the spot.</param>
    /// <param name="z">World Z of the spot.</param>
    /// <param name="fallbackRadius">Radius when no active ward covers the spot; must be &gt; 0.</param>
    /// <exception cref="ArgumentNullException"><paramref name="wards"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fallbackRadius"/> is not positive.</exception>
    public static NearbyArea Around(IEnumerable<Ward> wards, float x, float z, float fallbackRadius = DefaultRadius)
    {
        if (wards is null)
        {
            throw new ArgumentNullException(nameof(wards));
        }

        if (!(fallbackRadius > 0f) || float.IsInfinity(fallbackRadius))
        {
            throw new ArgumentOutOfRangeException(nameof(fallbackRadius), fallbackRadius, "Radius must be a finite number > 0.");
        }

        var covering = wards.Where(w => w.Enabled && w.Covers(x, z)).ToList();
        return new NearbyArea(covering, x, z, fallbackRadius);
    }

    /// <summary>Whether a point belongs to the area.</summary>
    public bool Contains(float x, float z)
    {
        if (IsWardArea)
        {
            return _wards.Any(w => w.Covers(x, z));
        }

        var dx = _x - x;
        var dz = _z - z;
        return Math.Sqrt((dx * dx) + (dz * dz)) <= _radius;
    }

    /// <summary>
    /// Port of <c>PrivateArea.CheckAccess</c>: a player may change things at a point if no active ward covers it,
    /// or if at least one active ward covering it lists them (builder or permitted player).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="wards"/> is null.</exception>
    public static bool CanAccess(IEnumerable<Ward> wards, float x, float z, long playerId)
    {
        if (wards is null)
        {
            throw new ArgumentNullException(nameof(wards));
        }

        var covered = false;
        foreach (var ward in wards)
        {
            if (!ward.Enabled || !ward.Covers(x, z))
            {
                continue;
            }

            if (ward.Grants(playerId))
            {
                return true;
            }

            covered = true;
        }

        return !covered;
    }
}
