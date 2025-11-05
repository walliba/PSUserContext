[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $XmlPath,
    [Parameter(Mandatory)] [string] $OutputFolder
)

[xml]$xml = Get-Content $XmlPath
$members = $xml.doc.members.member

foreach ($member in $members) {
    # Only handle cmdlets (classes derived from PSCmdlet)
    if ($member.name -match 'T:PSUserContext.Cmdlets\.(.+)Command$') {
        $cmdletName = $Matches[1] -replace 'Command$',''
        $summary    = $member.summary.Trim()
        $remarks    = $member.remarks.Trim()

        $out = @"
# $cmdletName

## SYNOPSIS
$summary

## DESCRIPTION
$remarks

## EXAMPLES
(Examples to be added)

## PARAMETERS
(Parameters auto-detected by PlatyPS)
"@

        $outPath = Join-Path $OutputFolder "$cmdletName.md"
        Set-Content -Path $outPath -Value $out -Encoding UTF8
    }
}