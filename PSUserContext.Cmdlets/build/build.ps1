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

$ManifestTemplate = Get-Content -Path .\module\PSUserContext.psd1.template -Raw

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

$ManifestTemplate = $ManifestTemplate.Replace('{{DESCRIPTION}}', "Proof-of-concept binary module for executing processes in alternate user contexts.")
$ManifestTemplate = $ManifestTemplate.Replace('{{AUTHOR}}', "Bryce Wallis")
$ManifestTemplate = $ManifestTemplate.Replace('{{MODULE_ROOT}}', $bName)
$ManifestTemplate = $ManifestTemplate.Replace('{{CMDLET_EXPORTS}}', ($bModule.ExportedCmdlets.Values.Name | % {"'$_'"}) -join ',')
$ManifestTemplate = $ManifestTemplate.Replace('{{MODULE_TYPES}}', "'PSUserContext.types.ps1xml'" -join ',')
$ManifestTemplate = $ManifestTemplate.Replace('{{MODULE_FORMATS}}', "'PSUserContext.formats.ps1xml'" -join ',')

$ManifestTemplate = Cleanup-Manifest($ManifestTemplate)

$BinaryModules = @(
    $bName, "PSUserContext.Api.dll"
) | % {[IO.Path]::Combine($binDir, $_)}

Copy-Item -Path .\module\PSUserContext.formats.ps1xml -Destination $ModulePath
Copy-Item -Path .\module\PSUserContext.types.ps1xml -Destination $ModulePath
Copy-Item $BinaryModules -Destination $ModulePath

Write-Host "[Manifest] Copied module dlls to $ModulePath"

$ManifestTemplate | Out-File -FilePath $ManifestPath -Encoding utf8

Write-Host "[Manifest] Generated module manifest for $Configuration"

