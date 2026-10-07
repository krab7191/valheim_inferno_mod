using System;
using System.Collections.Generic;

namespace Inferno.Core.Ownership;

/// <summary>
/// Remembers which server-owned objects are temporarily lent to a player and until when.
/// </summary>
/// <typeparam name="TKey">Object id type.</typeparam>
public sealed class LoanBook<TKey>
    where TKey : notnull
{
    private readonly Dictionary<TKey, double> _until = [];

    /// <summary>Number of active or not yet pruned loans.</summary>
    public int Count => _until.Count;

    /// <summary>Lends an object until <paramref name="now"/> + <paramref name="seconds"/> (extends an existing loan).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seconds"/> is not positive.</exception>
    public void Lend(TKey id, double now, double seconds)
    {
        if (!(seconds > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Loan time must be > 0.");
        }

        var until = now + seconds;
        _until[id] = _until.TryGetValue(id, out var existing) ? Math.Max(existing, until) : until;
    }

    /// <summary>Whether the object is lent at <paramref name="now"/>.</summary>
    public bool IsOnLoan(TKey id, double now) => _until.TryGetValue(id, out var until) && now < until;

    /// <summary>Forgets expired loans.</summary>
    public void Prune(double now)
    {
        List<TKey>? expired = null;
        foreach (var entry in _until)
        {
            if (now >= entry.Value)
            {
                (expired ??= []).Add(entry.Key);
            }
        }

        if (expired is null)
        {
            return;
        }

        foreach (var id in expired)
        {
            _until.Remove(id);
        }
    }

    /// <summary>Forgets all loans.</summary>
    public void Clear() => _until.Clear();
}
