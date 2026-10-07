using System;
using System.Collections.Generic;

namespace Inferno.Core.Commands;

/// <summary>
/// Recognizes repeats of the same command from the same player within a short time.
/// </summary>
/// <remarks>
/// A vanilla client sends a chat line separately to every other player, so the server relays one copy per
/// recipient. The command must run once, not once per player online.
/// </remarks>
public sealed class DuplicateFilter
{
    private readonly double _windowSeconds;
    private readonly Dictionary<string, double> _lastSeen = [];

    /// <summary>Creates a filter.</summary>
    /// <param name="windowSeconds">How long a repeat counts as a copy; must be &gt; 0.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="windowSeconds"/> is not positive.</exception>
    public DuplicateFilter(double windowSeconds)
    {
        if (!(windowSeconds > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(windowSeconds), windowSeconds, "Window must be > 0.");
        }

        _windowSeconds = windowSeconds;
    }

    /// <summary>
    /// Returns true the first time a (sender, text) pair is seen, and false for copies within the window.
    /// </summary>
    /// <param name="senderId">Unique id of the sender (e.g. network peer id).</param>
    /// <param name="text">The command text.</param>
    /// <param name="nowSeconds">Current time in seconds (any monotonic clock).</param>
    public bool IsFirst(long senderId, string text, double nowSeconds)
    {
        Prune(nowSeconds);

        var key = senderId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n" + text;
        if (_lastSeen.ContainsKey(key))
        {
            return false;
        }

        _lastSeen[key] = nowSeconds;
        return true;
    }

    /// <summary>Number of remembered entries (for tests and diagnostics).</summary>
    internal int Count => _lastSeen.Count;

    private void Prune(double nowSeconds)
    {
        List<string>? expired = null;
        foreach (var entry in _lastSeen)
        {
            if (nowSeconds - entry.Value >= _windowSeconds)
            {
                (expired ??= []).Add(entry.Key);
            }
        }

        if (expired is null)
        {
            return;
        }

        foreach (var key in expired)
        {
            _lastSeen.Remove(key);
        }
    }
}
