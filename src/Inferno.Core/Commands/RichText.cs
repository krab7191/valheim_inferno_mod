using System.Text.RegularExpressions;

namespace Inferno.Core.Commands;

/// <summary>
/// Valheim signs (and chat) accept rich-text tags such as <c>&lt;color=green&gt;</c>, <c>&lt;b&gt;</c> or
/// <c>&lt;size=40&gt;</c>. Commands are read without them; answers can reuse the player's formatting.
/// </summary>
public static class RichText
{
    private static readonly Regex Tag = new("<[^<>]*>", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex LeadingTags = new(@"^\s*((?:<[^<>/][^<>]*>\s*)+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Returns the text with every tag removed.</summary>
    public static string Strip(string? text) => text is null ? string.Empty : Tag.Replace(text, string.Empty);

    /// <summary>
    /// Returns the opening tags the text starts with (e.g. <c>&lt;color=green&gt;&lt;b&gt;</c>), or an empty string,
    /// so an answer can be written in the same style as the command.
    /// </summary>
    public static string Leading(string? text)
    {
        if (text is null)
        {
            return string.Empty;
        }

        var match = LeadingTags.Match(text);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }
}
