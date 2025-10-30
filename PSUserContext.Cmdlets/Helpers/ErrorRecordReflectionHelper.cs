using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Reflection;

namespace PSUserContext.Cmdlets.Helpers;

public class ErrorRecordReflectionHelper
{
    private static readonly MethodInfo? _fromPSObjectForRemoting;

    static ErrorRecordReflectionHelper()
    {
        // Locate the internal static method:
        // internal static ErrorRecord FromPSObjectForRemoting(PSObject serializedErrorRecord)
        _fromPSObjectForRemoting = typeof(ErrorRecord)
            .GetMethod("FromPSObjectForRemoting",
                BindingFlags.NonPublic | BindingFlags.Static);
    }

    /// <summary>
    /// Uses PowerShell's internal remoting deserializer to rehydrate a live ErrorRecord
    /// from a deserialized PSObject (Deserialized.System.Management.Automation.ErrorRecord).
    /// </summary>
    public static ErrorRecord? FromPsObject(PSObject serializedErrorRecord)
    {
        if (_fromPSObjectForRemoting == null)
            throw new MissingMethodException(
                "ErrorRecord.FromPSObjectForRemoting is not available in this PowerShell version.");
        
        // todo: implement InvocationInfo serialization
        // https://github.com/PowerShell/PowerShell/blob/master/src/System.Management.Automation/engine/InvocationInfo.cs#L90
        
        serializedErrorRecord = SerializeExtendedInfo(serializedErrorRecord);
        
        try
        {
            return (ErrorRecord?)_fromPSObjectForRemoting.Invoke(null, new object[] { serializedErrorRecord });
        }
        catch (TargetInvocationException tie)
        {
            throw tie.InnerException ?? tie;
        }
    }

    private static PSObject SerializeExtendedInfo(PSObject psoEr)
    {
        var invinfo = psoEr.Properties["InvocationInfo"].Value as PSObject;
        
        if (invinfo == null)
            throw new MissingMemberException("Cannot serialize extended info: information is missing");
        
        AddProperty(psoEr,"InvocationInfo_CommandOrigin", invinfo.Properties["CommandOrigin"]?.Value);
        AddProperty(psoEr,"InvocationInfo_ExpectingInput", invinfo.Properties["ExpectingInput"]?.Value);
        AddProperty(psoEr, "InvocationInfo_InvocationName", invinfo.Properties["InvocationName"]?.Value);
        AddProperty(psoEr, "InvocationInfo_HistoryId", invinfo.Properties["HistoryId"]?.Value);
        AddProperty(psoEr, "InvocationInfo_PipelineLength", invinfo.Properties["PipelineLength"]?.Value);
        AddProperty(psoEr, "InvocationInfo_PipelinePosition", invinfo.Properties["PipelinePosition"]?.Value);
        
        AddProperty(psoEr, "InvocationInfo_ScriptName", invinfo.Properties["ScriptName"]?.Value);
        AddProperty(psoEr, "InvocationInfo_ScriptLineNumber", invinfo.Properties["ScriptLineNumber"]?.Value);
        AddProperty(psoEr, "InvocationInfo_OffsetInLine", invinfo.Properties["OffsetInLine"]?.Value);
        AddProperty(psoEr, "InvocationInfo_Line", invinfo.Properties["Line"]?.Value);
        
        
        AddProperty(psoEr, "InvocationInfo_PipelineIterationInfo", invinfo.Properties["PipelineIterationInfo"]?.Value);
        
        AddProperty(psoEr, "InvocationInfo_BoundParameters", invinfo.Properties["BoundParameters"]?.Value);
        AddProperty(psoEr, "InvocationInfo_UnboundParameters", invinfo.Properties["UnboundParameters"]?.Value);

        psoEr.Properties["SerializeExtendedInfo"].Value = true;
        
        return psoEr;

        static void AddProperty(PSObject psObj, string name, object? value)
        {
            if (psObj.Properties[name] == null)
                psObj.Properties.Add(new PSNoteProperty(name, value));
        }
    }
}