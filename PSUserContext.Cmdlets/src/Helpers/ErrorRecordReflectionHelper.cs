using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Reflection;

namespace PSUserContext.Cmdlets.Helpers;

public static class ErrorRecordReflectionHelper
{
    private static readonly MethodInfo? FromPsObjectForRemoting;

    static ErrorRecordReflectionHelper()
    {
        // Locate the internal static method:
        // internal static ErrorRecord FromPSObjectForRemoting(PSObject serializedErrorRecord)
        FromPsObjectForRemoting = typeof(ErrorRecord)
            .GetMethod("FromPSObjectForRemoting",
                BindingFlags.NonPublic | BindingFlags.Static);
    }

    /// <summary>
    /// Uses PowerShell's internal remoting deserializer to rehydrate a live ErrorRecord
    /// from a deserialized PSObject (Deserialized.System.Management.Automation.ErrorRecord).
    /// </summary>
    public static ErrorRecord? FromPsObject(PSObject serializedErrorRecord)
    {
        if (FromPsObjectForRemoting == null)
            throw new MissingMethodException(
                "ErrorRecord.FromPSObjectForRemoting is not available in this PowerShell version.");
        
        try
        {
            return (ErrorRecord?)FromPsObjectForRemoting.Invoke(null, [serializedErrorRecord]);
        }
        catch (TargetInvocationException tie)
        {
            throw tie.InnerException ?? tie;
        }
    }
}