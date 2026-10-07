using System;
using System.Collections.Generic;

namespace Inferno.Core.Commands;

/// <summary>Shortens long replies for on-screen messages, which can only show a few lines.</summary>
public static class ReplyLimiter
{
    /// <summary>
    /// Returns at most <paramref name="maxLines"/> lines. If lines were cut, the last line says how many and
    /// points to the F5 console, which shows everything.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLines"/> is less than 2.</exception>
    public static IReadOnlyList<string> Limit(IReadOnlyList<string> lines, int maxLines)
    {
        if (lines is null)
        {
            throw new ArgumentNullException(nameof(lines));
        }

        if (maxLines < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLines), maxLines, "Need room for at least one line plus the note.");
        }

        if (lines.Count <= maxLines)
        {
            return lines;
        }

        var kept = maxLines - 1;
        var result = new List<string>(maxLines);
        for (var i = 0; i < kept; i++)
        {
            result.Add(lines[i]);
        }

        result.Add($"… {lines.Count - kept} more line(s). Use the F5 console ('{CommandParser.ConsolePrefix} …') to see everything.");
        return result;
    }
}
