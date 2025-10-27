using System;
using System.IO;
using System.Management.Automation;

namespace PSUserContext.Cmdlets.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ValidateScriptFileAttribute : ValidateArgumentsAttribute
{
    protected override void Validate(object arguments, EngineIntrinsics engineIntrinsics)
    {
        if (arguments is not FileInfo file)
            throw new ValidationMetadataException("Invalid file type. Expected a FileInfo object.");
        
        if (!file.Exists)
            throw new ValidationMetadataException($"File not found: {file.FullName}");
        
        if (!string.Equals(Path.GetExtension(file.FullName), ".ps1", StringComparison.OrdinalIgnoreCase))
            throw new ValidationMetadataException("Only .ps1 files are supported.");
    }
}