namespace Inferno.Game;

/// <summary>
/// Display hints for the ConfigurationManager (F1) settings window. ConfigurationManager finds this class by name
/// and reads its public fields, so Inferno needs no reference to it (it stays an optional, separate plugin).
/// </summary>
#pragma warning disable CA1051, SA1401 // Field layout is ConfigurationManager's documented convention.
internal sealed class ConfigurationManagerAttributes
{
    /// <summary>Shows the setting but blocks editing (e.g. for non-admins while AdminOnly is on).</summary>
    public bool? ReadOnly;
}
#pragma warning restore CA1051, SA1401
