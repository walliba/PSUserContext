using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Remoting;
using System.Text;

namespace PSUserContext.Cmdlets.Helpers;

public static class CliXml
{
    private const string LineStart = "#< CLIXML";

    public static Exception CreateDynamicException(string typeName, string message)
    {
        // Try to resolve type name from any loaded assembly
        var type = Type.GetType(typeName, throwOnError: false);
        if (type == null)
        {
            // Try PowerShell assembly explicitly if not already loaded
            type = Type.GetType($"{typeName}, System.Management.Automation", throwOnError: false);
        }

        if (type == null || !typeof(Exception).IsAssignableFrom(type))
            throw new ArgumentException($"Invalid exception type: {typeName}");

        // Prefer a (string message) constructor, otherwise fallback to parameterless
        var ctor = type.GetConstructor(new[] { typeof(string) })
                   ?? type.GetConstructor(Type.EmptyTypes);

        if (ctor == null)
            throw new MissingMethodException($"No usable constructor found for {typeName}");

        var ex = ctor.GetParameters().Length == 1
            ? (Exception)ctor.Invoke(new object[] { message })
            : (Exception)ctor.Invoke(null);

        return ex;
    }
    
    public static ErrorRecord? ConvertToErrorRecord(object obj)
    {
        // Case 1: Direct ErrorRecord
        if (obj is ErrorRecord er)
            return er;

        // Case 2: PSObject wrapper
        if (obj is PSObject psObj)
        {
            // If the BaseObject is an ErrorRecord
            if (psObj.BaseObject is ErrorRecord er2)
                return er2;

            // Otherwise, attempt to rebuild from its properties
            // (Deserialized.* case)
            var typeName = psObj.TypeNames.Count > 0 ? psObj.TypeNames[0] : null;
            if (string.Equals(typeName, "Deserialized.System.Management.Automation.ErrorRecord",
                              StringComparison.OrdinalIgnoreCase))
            {
                var ex = GetNestedException(psObj);
                string fqid = psObj.Properties["FullyQualifiedErrorId"]?.Value?.ToString() ?? "RemoteError";
                Console.WriteLine($"fqid: {fqid}");
                object target = psObj.Properties["TargetObject"]?.Value;
                Console.WriteLine($"target: {target}");
                var category = ErrorCategory.NotSpecified;
                
                if (psObj.Properties["ErrorCategory_Category"]?.Value is int ec)
                {
                    category = (ErrorCategory)ec;
                }
                
                var err = new ErrorRecord(ex ?? new RemoteException("Unknown error"),
                                          fqid,
                                          category,
                                          target);
                
                var detailsMsg = (psObj.Properties["ErrorDetails"]?.Value as PSObject)?
                                 .Properties["Message"]?.Value?.ToString();
                if (!string.IsNullOrEmpty(detailsMsg))
                    err.ErrorDetails = new ErrorDetails(detailsMsg);

                var invocationInfo = (psObj.Properties["InvocationInfo"]?.Value as InvocationInfo);
                
                
                return err;
            }
        }

        return null;
    }

    // Helper: extract nested exception from PSObject
    private static Exception? GetNestedException(PSObject ps)
    {
        if (ps.Properties["Exception"]?.Value is PSObject exPs)
        {
            var msg = exPs.Properties["Message"]?.Value?.ToString();
            return new ParentContainsErrorRecordException(msg);
        }
        return null;
    }

    public static ErrorRecord[]? DeserializeCliXmlError(StringBuilder sb)
    {
        if (sb.Length == 0)
            return null;

        string xml = sb.ToString();
        if (string.IsNullOrWhiteSpace(xml))
            return null;
        
        // sanity check
        if (!xml.StartsWith(LineStart, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Data is not serialized CLIXML");
        
        int idx = xml.IndexOf("<Objs", StringComparison.OrdinalIgnoreCase);

        if (idx >= 0)
            xml = xml.Substring(idx);
        
        try
        {
            var objects = PSSerializer.DeserializeAsList(xml);

            List<ErrorRecord> errors = new List<ErrorRecord>();

            foreach (var o in objects)
            {
                var err = ConvertToErrorRecord(o);
                if (err is not null)
                    errors.Add(err);
            }
            
            return errors.ToArray();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error deserializing CLIXML", ex);
        }
    }
    
    public static object[]? DeserializeCliXml(StringBuilder sb)
    {
        if (sb.Length == 0)
            return null;

        string xml = sb.ToString();
        if (string.IsNullOrWhiteSpace(xml))
            return null;
        
        // sanity check
        if (!xml.StartsWith(LineStart, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Data is not serialized CLIXML");
        
        int idx = xml.IndexOf("<Objs", StringComparison.OrdinalIgnoreCase);

        if (idx >= 0)
            xml = xml.Substring(idx);
        
        try
        {
            return PSSerializer.DeserializeAsList(xml);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error deserializing CLIXML", ex);
        }
    }
}