using System;
using System.Management.Automation;
using System.Management.Automation.Remoting;
using System.Text;

namespace PSUserContext.Cmdlets.Helpers;

public static class CliXml
{
    private const string LineStart = "#< CLIXML";

    public static ErrorRecord? HydrateErrorRecord(object ps)
    {
        if ((ps is ErrorRecord errorRecord) &&
            (errorRecord.Exception != null) &&
            (errorRecord.Exception.InnerException != null))
        {
            if (errorRecord.Exception.InnerException is PSDirectException ex)
            {
                return new ErrorRecord(errorRecord.Exception.InnerException,
                    errorRecord.FullyQualifiedErrorId,
                    errorRecord.CategoryInfo.Category,
                    errorRecord.TargetObject);
            }

            return null;
        }

        return null;
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