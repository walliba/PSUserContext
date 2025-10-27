using System;
using PSUserContext.Api.Extensions;
using PSUserContext.Api.Interop;

namespace PSUserContext.Cmdlets.Helpers;

public static class PrivilegeHelper
{
    public static void CheckPrivilege(string privilegeName)
    {
        // Retrieve token privileges dictionary
        var privileges = TokenExtensions.GetTokenPrivileges();

        // Try to get the specific privilege
        if (!privileges.TryGetValue(privilegeName, out var privilegeAttr) ||
            (privilegeAttr == InteropTypes.PrivilegeAttributes.Disabled))
        {
            throw new InvalidOperationException(
                "Not running with correct privilege. You must run this script as SYSTEM " +
                "or have the SeDelegateSessionUserImpersonatePrivilege token.");
        }
    }
}