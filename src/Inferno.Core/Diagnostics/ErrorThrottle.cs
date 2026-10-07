using System;
using System.Collections.Generic;

namespace Inferno.Core.Diagnostics;

/// <summary>What to do with one occurrence of an error.</summary>
public readonly struct ErrorLogDecision
{
    internal ErrorLogDecision(bool log, bool firstTime, int suppressed)
    {
        Log = log;
        FirstTime = firstTime;
        Suppressed = suppressed;
    }

    /// <summary>Whether to write this occurrence to the log.</summary>
    public bool Log { get; }

    /// <summary>True for the very first occurrence: log it in full (with stack trace).</summary>
    public bool FirstTime { get; }

    /// <summary>Repeats that were not logged since the last logged occurrence.</summary>
    public int Suppressed { get; }
}

/// <summary>
/// Keeps repeating errors from flooding the log: each kind of error (key) is logged in full the first time, then at
/// most once per interval with the number of repeats in between. Every occurrence is still counted.
/// </summary>
public sealed class ErrorThrottle
{
    private readonly double _intervalSeconds;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>Creates a throttle.</summary>
    /// <param name="intervalSeconds">Minimum time between two log lines for the same error; must be &gt; 0.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="intervalSeconds"/> is not positive.</exception>
    public ErrorThrottle(double intervalSeconds)
    {
        if (!(intervalSeconds > 0) || double.IsInfinity(intervalSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(intervalSeconds), intervalSeconds, "Interval must be a finite number > 0.");
        }

        _intervalSeconds = intervalSeconds;
    }

    /// <summary>Total errors seen since start (logged or not).</summary>
    public int Total { get; private set; }

    /// <summary>Number of different errors seen.</summary>
    public int Distinct => _entries.Count;

    /// <summary>Records one occurrence of an error and decides whether to log it.</summary>
    /// <param name="key">Identifies the kind of error, e.g. "scan:piece_bathtub:NullReferenceException".</param>
    /// <param name="nowSeconds">Current time in seconds (monotonic).</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public ErrorLogDecision Record(string key, double nowSeconds)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        Total++;
        if (!_entries.TryGetValue(key, out var entry))
        {
            _entries[key] = new Entry { LastLogged = nowSeconds };
            return new ErrorLogDecision(log: true, firstTime: true, suppressed: 0);
        }

        if (nowSeconds - entry.LastLogged < _intervalSeconds)
        {
            entry.Suppressed++;
            return new ErrorLogDecision(log: false, firstTime: false, suppressed: entry.Suppressed);
        }

        var suppressed = entry.Suppressed;
        entry.Suppressed = 0;
        entry.LastLogged = nowSeconds;
        return new ErrorLogDecision(log: true, firstTime: false, suppressed: suppressed);
    }

    private sealed class Entry
    {
        public double LastLogged { get; set; }

        public int Suppressed { get; set; }
    }
}
