param(
    [Parameter(Mandatory)][string] $binDir,
    [Parameter(Mandatory)][string] $Configuration
)

$ModuleName = "PSUserContext"
$bName = "PSUserContext.Cmdlets.dll"
[string] $bPath = [IO.Path]::Combine($binDir, $bName)
[string] $ModulePath = [IO.Path]::Combine($binDir, $ModuleName)
[string] $ManifestPath = [IO.Path]::Combine($ModulePath, "$ModuleName.psd1")
$bModule = Import-Module $bPath -PassThru


$ManifestTemplate = @"
@{
RootModule = '{{MODULE_ROOT}}'
ModuleVersion = '1.0'
CompatiblePSEditions = @('Desktop', 'Core')
GUID = '3bb4a8f8-2973-45ed-8916-6fe74a804fca'
Author = '{{AUTHOR}}'
# CompanyName = 'Unknown'
Copyright = 'Copyright (c) 2025 Bryce Wallis'
Description = '{{DESCRIPTION}}'
PowerShellVersion = '5.0'
# Minimum version of Microsoft .NET Framework required by this module. This prerequisite is valid for the PowerShell Desktop edition only.
# DotNetFrameworkVersion = ''
# Minimum version of the common language runtime (CLR) required by this module. This prerequisite is valid for the PowerShell Desktop edition only.
# CLRVersion = ''
# Modules that must be imported into the global environment prior to importing this module
# RequiredModules = @()
# RequiredAssemblies = @()
# ScriptsToProcess = @()
# TypesToProcess = @()
# FormatsToProcess = @()
# NestedModules = @()
# FunctionsToExport = '*'
CmdletsToExport = @({{CMDLET_EXPORTS}})
# VariablesToExport = '*'
AliasesToExport = '*'
# DscResourcesToExport = @()
# ModuleList = @()
# FileList = @()
# Private data to pass to the module specified in RootModule/ModuleToProcess. This may also contain a PSData hashtable with additional module metadata used by PowerShell.
PrivateData = @{

    PSData = @{

        # Tags applied to this module. These help with module discovery in online galleries.
        # Tags = @()

        # A URL to the license for this module.
        # LicenseUri = ''

        # A URL to the main website for this project.
        # ProjectUri = ''

        # A URL to an icon representing this module.
        # IconUri = ''

        # ReleaseNotes of this module
        # ReleaseNotes = ''
    }
}

# HelpInfo URI of this module
# HelpInfoURI = ''

# Default prefix for commands exported from this module. Override the default prefix using Import-Module -Prefix.
# DefaultCommandPrefix = ''
}
"@


function Cleanup-Manifest
{
    param (
        [string] $content
    )
    
    $builder = [System.Text.StringBuilder]::new()
    
    foreach ($line in $content.Split("`r?`n"))
    {
        $line = $line.TrimEnd("`r")
        if ($line.Trim() -and $line -notmatch '^\s*#')
        {
            [void]$builder.AppendLine($line)
        }
    }
    
    $builder.ToString()
}

$ManifestTemplate = $ManifestTemplate.Replace('{{DESCRIPTION}}', "This is my description")
$ManifestTemplate = $ManifestTemplate.Replace('{{AUTHOR}}', "Bryce Wallis")
$ManifestTemplate = $ManifestTemplate.Replace('{{MODULE_ROOT}}', $bName)
$ManifestTemplate = $ManifestTemplate.Replace('{{CMDLET_EXPORTS}}', ($bModule.ExportedCmdlets.Values.Name | % {"'$_'"}) -join ',')

$ManifestTemplate = Cleanup-Manifest($ManifestTemplate)

$BinaryModules = @(
    $bName, "PSUserContext.Api.dll"
) | % {[IO.Path]::Combine($binDir, $_)}

Copy-Item $BinaryModules -Destination $ModulePath

Write-Host "[Manifest] Copied module dlls to $ModulePath"

$ManifestTemplate | Out-File -FilePath $ManifestPath -Encoding utf8

Write-Host "[Manifest] Generated module manifest for $Configuration"

