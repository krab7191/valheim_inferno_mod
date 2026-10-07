using Inferno.Core.Permissions;
using Xunit;

namespace Inferno.Core.Tests.Permissions;

public class PermissionPolicyTests
{
    [Theory]
    [InlineData(true, false, true)]   // admin, AdminOnly off
    [InlineData(true, true, true)]    // admin, AdminOnly on: admins can always change, incl. turning AdminOnly off
    [InlineData(false, false, true)]  // anyone while AdminOnly is off, incl. turning AdminOnly on
    [InlineData(false, true, false)]  // non-admin can't change anything (or turn AdminOnly off) while it is on
    public void CanChangeSettings(bool isAdmin, bool adminOnly, bool expected) =>
        Assert.Equal(expected, PermissionPolicy.CanChangeSettings(isAdmin, adminOnly));
}
