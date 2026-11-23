[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RepositoryPath,
    [ValidateRange(1, 100)][int]$RetentionCount = 7,
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$repository = [IO.Path]::GetFullPath($RepositoryPath)
if (-not (Test-Path -LiteralPath $repository -PathType Container)) { throw "Backup repository does not exist." }
if (-not (Test-Path -LiteralPath (Join-Path $repository ".todir-backup-repository") -PathType Leaf)) { throw "Backup repository marker is missing; refusing deletion." }
if ([IO.Path]::GetPathRoot($repository) -eq $repository) { throw "Refusing to operate on a filesystem root." }
$entries = @()
foreach ($manifestPath in Get-ChildItem -LiteralPath $repository -Filter '*.manifest.json' -File) {
    try { $manifest = Get-Content -Raw -LiteralPath $manifestPath.FullName | ConvertFrom-Json } catch { continue }
    $artifactName = [string]$manifest.artifact
    if ($manifest.status -ne 'verified' -or $artifactName -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,180}$') { continue }
    $artifact = Join-Path $repository $artifactName
    if (-not (Test-Path -LiteralPath $artifact -PathType Leaf)) { continue }
    $entries += [pscustomobject]@{ Manifest = $manifestPath.FullName; Artifact = $artifact; Created = [DateTimeOffset]::Parse($manifest.createdAtUtc) }
}
$delete = $entries | Sort-Object Created -Descending | Select-Object -Skip $RetentionCount
foreach ($entry in $delete) {
    if (-not $WhatIf) { Remove-Item -LiteralPath $entry.Artifact -Force; Remove-Item -LiteralPath $entry.Manifest -Force }
    Write-Output ([ordered]@{ action = if ($WhatIf) { 'would-delete' } else { 'deleted' }; artifact = [IO.Path]::GetFileName($entry.Artifact); createdAtUtc = $entry.Created.ToString('O') } | ConvertTo-Json -Compress)
}
