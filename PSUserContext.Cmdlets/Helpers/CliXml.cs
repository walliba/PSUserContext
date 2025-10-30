using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Remoting;
using System.Management.Automation.Remoting.Internal;
using System.Reflection;
using System.Text;

namespace PSUserContext.Cmdlets.Helpers;

public static class CliXml
{
    private const string LineStart = "#< CLIXML";

    public static List<ErrorRecord>? DeserializeCliXmlError(StringBuilder sb)
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
                if (o is PSObject pso)
                {
                    if (pso.TypeNames.Contains("Deserialized.System.Management.Automation.ErrorRecord"))
                    {
                        var err = ErrorRecordReflectionHelper.FromPsObject(pso);
                        if (err is not null)
                            errors.Add(err);
                    }

                }
            }

            return errors;
        }
        catch (Exception ex)
        {
            // do nothing
            // throw new InvalidOperationException($"Error deserializing CLIXML: {ex.Message}", ex);
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