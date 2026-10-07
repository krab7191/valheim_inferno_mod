using System;
using System.Collections.Generic;

namespace Inferno.Core.Commands;

/// <summary>Result of asking the rate limiter.</summary>
public enum RateDecision
{
    /// <summary>Within the limit: run the command.</summary>
    Allow,

    /// <summary>Over the limit for the first time since the player was last allowed: tell them once.</summary>
    DenyAndWarn,

    /// <summary>Still over the limit: drop silently (no reply, no log spam).</summary>
    DenySilently,
}

/// <summary>
/// Per-player token bucket shared by every command source (chat, signs, console, menu). Allows a short burst,
/// then a steady rate, so normal use is never blocked while spam can't flood the server or its log.
/// </summary>
public sealed class RateLimiter
{
    private readonly double _capacity;
    private readonly double _refillPerSecond;
    private readonly Dictionary<string, Bucket> _buckets = new(StringComparer.Ordinal);

    /// <summary>Creates a limiter.</summary>
    /// <param name="burst">Commands a player may send at once; must be ≥ 1.</param>
    /// <param name="refillPerSecond">Commands per second regained afterwards; must be &gt; 0.</param>
    /// <exception cref="ArgumentOutOfRangeException">An argument is out of range.</exception>
    public RateLimiter(int burst, double refillPerSecond)
    {
        if (burst < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(burst), burst, "Burst must be ≥ 1.");
        }

        if (!(refillPerSecond > 0) || double.IsInfinity(refillPerSecond))
        {
            throw new ArgumentOutOfRangeException(nameof(refillPerSecond), refillPerSecond, "Refill rate must be a finite number > 0.");
        }

        _capacity = burst;
        _refillPerSecond = refillPerSecond;
    }

    /// <summary>Number of players currently tracked (full buckets are forgotten).</summary>
    public int Count => _buckets.Count;

    /// <summary>Takes one command from a player's bucket.</summary>
    /// <param name="playerKey">Stable player id (platform id).</param>
    /// <param name="nowSeconds">Current time in seconds (monotonic).</param>
    /// <exception cref="ArgumentNullException"><paramref name="playerKey"/> is null.</exception>
    public RateDecision TryTake(string playerKey, double nowSeconds)
    {
        if (playerKey is null)
        {
            throw new ArgumentNullException(nameof(playerKey));
        }

        Prune(nowSeconds);
        if (!_buckets.TryGetValue(playerKey, out var bucket))
        {
            bucket = new Bucket { Tokens = _capacity, Updated = nowSeconds };
            _buckets[playerKey] = bucket;
        }

        Refill(bucket, nowSeconds);
        if (bucket.Tokens >= 1.0)
        {
            bucket.Tokens -= 1.0;
            bucket.Warned = false;
            return RateDecision.Allow;
        }

        if (bucket.Warned)
        {
            return RateDecision.DenySilently;
        }

        bucket.Warned = true;
        return RateDecision.DenyAndWarn;
    }

    private void Refill(Bucket bucket, double now)
    {
        var elapsed = Math.Max(0.0, now - bucket.Updated);
        bucket.Tokens = Math.Min(_capacity, bucket.Tokens + (elapsed * _refillPerSecond));
        bucket.Updated = now;
    }

    // Buckets that would be full again carry no information; dropping them keeps memory bounded.
    private void Prune(double now)
    {
        List<string>? full = null;
        foreach (var entry in _buckets)
        {
            var bucket = entry.Value;
            if (bucket.Tokens + ((now - bucket.Updated) * _refillPerSecond) >= _capacity)
            {
                (full ??= []).Add(entry.Key);
            }
        }

        if (full is null)
        {
            return;
        }

        foreach (var key in full)
        {
            _buckets.Remove(key);
        }
    }

    private sealed class Bucket
    {
        public double Tokens { get; set; }

        public double Updated { get; set; }

        public bool Warned { get; set; }
    }
}
