using System;

namespace Inferno.Core.Commands;

/// <summary>The player who sent a command.</summary>
public sealed class CommandSender
{
    /// <summary>Creates a sender.</summary>
    /// <param name="name">Character name, for replies and the log.</param>
    /// <param name="platformId">Platform user id (Steam / Xbox), for the log.</param>
    /// <param name="isAdmin">Whether the player is on the server's admin list.</param>
    public CommandSender(string name, string platformId, bool isAdmin)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        PlatformId = platformId ?? throw new ArgumentNullException(nameof(platformId));
        IsAdmin = isAdmin;
    }

    /// <summary>Character name.</summary>
    public string Name { get; }

    /// <summary>Platform user id.</summary>
    public string PlatformId { get; }

    /// <summary>Whether the player is on the server's admin list.</summary>
    public bool IsAdmin { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Name} ({PlatformId}{(IsAdmin ? ", admin" : string.Empty)})";
}
