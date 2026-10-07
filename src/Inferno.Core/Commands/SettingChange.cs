namespace Inferno.Core.Commands;

/// <summary>One changed setting, for the audit log.</summary>
public sealed class SettingChange
{
    /// <summary>Creates a change record.</summary>
    public SettingChange(string section, string setting, string oldValue, string newValue)
    {
        Section = section;
        Setting = setting;
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>Config section: <c>General</c> or a prefab name.</summary>
    public string Section { get; }

    /// <summary>Setting name.</summary>
    public string Setting { get; }

    /// <summary>Value before the change.</summary>
    public string OldValue { get; }

    /// <summary>Value after the change.</summary>
    public string NewValue { get; }

    /// <inheritdoc />
    public override string ToString() => $"[{Section}] {Setting}: {OldValue} -> {NewValue}";
}
