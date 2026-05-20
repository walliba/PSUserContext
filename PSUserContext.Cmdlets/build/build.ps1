param(
    [Parameter(Mandatory)][string] $binDir,
    [Parameter(Mandatory)][string] $Configuration
)

$ModuleName = "PSUserContext"
$bName = "PSUserContext.Cmdlets.dll"

[string] $bPath = [IO.Path]::Combine($binDir, $bName)
[string] $ModulePath = [IO.Path]::Combine($binDir, $ModuleName)
[string] $ModuleBin = [IO.Path]::Combine($ModulePath, 'bin')
[string] $ManifestPath = [IO.Path]::Combine($ModulePath, "$ModuleName.psd1")
$bModule = Import-Module $bPath -PassThru

$BinaryModules = @(
    $bName, "PSUserContext.Api.dll"
)

if ((Split-Path ($binDir.TrimEnd('\')) -Leaf) -like "net4*")
{
    $BinaryModules = @(
        $bName, "PSUserContext.Api.dll", "System.Memory.dll", "System.Runtime.CompilerServices.Unsafe.dll", "System.Numerics.Vectors.dll"
    )
}

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

function Get-RelativeBin
{
    param(
        [string]$file
    )
    
    return ".\bin\{0}" -f $file
}

function Copy-Binaries 
{
    if (-not (Test-Path -Path $ModuleBin))
    {
        $null = New-Item -ItemType Directory -Path $ModuleBin -Force
    }
    
    $BinaryModulesAbsolute = $BinaryModules | % {[IO.Path]::Combine($binDir, $_)}
    
    Copy-Item -Path $BinaryModulesAbsolute -Destination $ModuleBin

    Write-Host "[Manifest] Copied module dlls to $ModulePath"
}

$ManifestTemplate = Get-Content -Path .\module\PSUserContext.psd1.template -Raw

$ManifestTemplate = $ManifestTemplate.Replace('{{DESCRIPTION}}', "Proof-of-concept binary module for executing processes in alternate user contexts.")
$ManifestTemplate = $ManifestTemplate.Replace('{{AUTHOR}}', "Bryce Wallis")
$ManifestTemplate = $ManifestTemplate.Replace('{{MODULE_ROOT}}', ".\bin\{0}" -f $bName)
$ManifestTemplate = $ManifestTemplate.Replace('{{MODULE_ASSEMBLIES}}', ($BinaryModules | Where-Object {$_ -ne $bName} | % {"'$(Get-RelativeBin -file $_)'"}) -join ',')
$ManifestTemplate = $ManifestTemplate.Replace('{{CMDLET_EXPORTS}}', ($bModule.ExportedCmdlets.Values.Name | % {"'$_'"}) -join ',')
$ManifestTemplate = $ManifestTemplate.Replace('{{MODULE_TYPES}}', "'PSUserContext.types.ps1xml'" -join ',')
$ManifestTemplate = $ManifestTemplate.Replace('{{MODULE_FORMATS}}', "'PSUserContext.formats.ps1xml'" -join ',')

$ManifestTemplate = Cleanup-Manifest($ManifestTemplate)

Copy-Binaries

Copy-Item -Path .\module\PSUserContext.formats.ps1xml -Destination $ModulePath
Copy-Item -Path .\module\PSUserContext.types.ps1xml -Destination $ModulePath

$ManifestTemplate | Out-File -FilePath $ManifestPath -Encoding utf8

Write-Host "[Manifest] Generated module manifest for $Configuration"

