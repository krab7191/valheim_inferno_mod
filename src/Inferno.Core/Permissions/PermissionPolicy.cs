namespace Inferno.Core.Permissions;

/// <summary>
/// Who may change Inferno's settings.
/// </summary>
/// <remarks>
/// Admins (from the server's own admin list, which Inferno never changes) may always change settings.
/// Everyone else may change settings only while <c>AdminOnly</c> is off. This one rule means anyone can turn
/// <c>AdminOnly</c> on, but only an admin can turn it off again.
/// </remarks>
public static class PermissionPolicy
{
    /// <summary>Whether a player may change settings.</summary>
    /// <param name="isAdmin">Whether the player is on the server's admin list.</param>
    /// <param name="adminOnly">The current value of the <c>AdminOnly</c> setting.</param>
    public static bool CanChangeSettings(bool isAdmin, bool adminOnly) => isAdmin || !adminOnly;
}
