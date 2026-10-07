using Inferno.Core.Time;

namespace Inferno.Core.Commands;

/// <summary>A syntactically valid <c>!fires</c> command. Targets are not checked against the catalog yet.</summary>
public sealed class ParsedCommand
{
    internal ParsedCommand(CommandKind kind, string? target = null, bool flag = false, int number = 0, DailySchedule schedule = default, string? name = null)
    {
        Kind = kind;
        Target = target;
        Flag = flag;
        Number = number;
        Schedule = schedule;
        Name = name;
    }

    /// <summary>The preset name of <see cref="CommandKind.Preset"/>.</summary>
    public string? Name { get; }

    /// <summary>Which command.</summary>
    public CommandKind Kind { get; }

    /// <summary>Target (prefab name or group), or null when the command has none.</summary>
    public string? Target { get; }

    /// <summary>The on/off value of on/off commands.</summary>
    public bool Flag { get; }

    /// <summary>The burn rate level of <see cref="CommandKind.BurnRate"/>.</summary>
    public int Number { get; }

    /// <summary>The schedule of <see cref="CommandKind.Schedule"/>.</summary>
    public DailySchedule Schedule { get; }
}
