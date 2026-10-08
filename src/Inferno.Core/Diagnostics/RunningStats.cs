using System;
using System.Globalization;

namespace Inferno.Core.Diagnostics;

/// <summary>Count, average and maximum of a series of timings (milliseconds), for the periodic performance log.</summary>
public sealed class RunningStats
{
    private double _total;

    /// <summary>Number of samples since the last reset.</summary>
    public int Count { get; private set; }

    /// <summary>Largest sample since the last reset (0 when empty).</summary>
    public double Max { get; private set; }

    /// <summary>Average sample since the last reset (0 when empty).</summary>
    public double Average => Count == 0 ? 0 : _total / Count;

    /// <summary>Adds a sample. Negative or non-finite values are ignored.</summary>
    public void Add(double milliseconds)
    {
        if (!(milliseconds >= 0) || double.IsInfinity(milliseconds))
        {
            return;
        }

        Count++;
        _total += milliseconds;
        Max = Math.Max(Max, milliseconds);
    }

    /// <summary>Starts a new period.</summary>
    public void Reset()
    {
        Count = 0;
        _total = 0;
        Max = 0;
    }

    /// <summary>E.g. "120 × avg 0.42 ms, max 1.80 ms".</summary>
    public override string ToString() =>
        string.Format(CultureInfo.InvariantCulture, "{0} × avg {1:0.00} ms, max {2:0.00} ms", Count, Average, Max);
}
