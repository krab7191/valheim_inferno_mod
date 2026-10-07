using System;
using System.Collections.Generic;

namespace Inferno.Core.Commands;

/// <summary>
/// The short answer Inferno writes back onto a sign after running a command written on it, so the result stays
/// readable (on-screen messages fade within seconds). The next command written on the sign replaces it.
/// </summary>
public static class SignReply
{
    /// <summary>Longest answer written to a sign (a vanilla sign accepts 50 characters of input).</summary>
    public const int MaxLength = 50;

    /// <summary>Returns the first reply line, shortened to fit a sign; empty when there is no reply.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="reply"/> is null.</exception>
    public static string Summarize(IReadOnlyList<string> reply)
    {
        if (reply is null)
        {
            throw new ArgumentNullException(nameof(reply));
        }

        if (reply.Count == 0)
        {
            return string.Empty;
        }

        var first = reply[0].Trim();
        return first.Length <= MaxLength ? first : first.Substring(0, MaxLength - 1) + "…";
    }
}
