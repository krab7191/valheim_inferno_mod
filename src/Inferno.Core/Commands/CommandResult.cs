using System.Collections.Generic;

namespace Inferno.Core.Commands;

/// <summary>What happened when a command ran.</summary>
public sealed class CommandResult
{
    internal CommandResult(IReadOnlyList<string> reply, IReadOnlyList<SettingChange> changes, bool denied)
    {
        Reply = reply;
        Changes = changes;
        Denied = denied;
    }

    /// <summary>Lines to send back to the player who ran the command (only to them).</summary>
    public IReadOnlyList<string> Reply { get; }

    /// <summary>Settings that changed, for the audit log. Empty when nothing changed.</summary>
    public IReadOnlyList<SettingChange> Changes { get; }

    /// <summary>True when the player was not allowed to run the command.</summary>
    public bool Denied { get; }
}
