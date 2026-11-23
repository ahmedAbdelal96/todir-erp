[CmdletBinding()]
param(
    [string]$ArtifactsRoot = (Join-Path (Get-Location) "artifacts"),
    [ValidateRange(1, 100)][int]$RetentionCount = 10,
    [switch]$Delete,
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$root = [IO.Path]::GetFullPath($ArtifactsRoot)
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts"))
if ($root -ne $allowedRoot -and -not $root.StartsWith($allowedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "ArtifactsRoot must be under the ignored repository artifacts directory." }
if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw "ArtifactsRoot does not exist." }

$runs = [Collections.Generic.List[object]]::new()
foreach ($directory in Get-ChildItem -LiteralPath $root -Directory) {
    $ownerPath = Join-Path $directory.FullName ".todir-recovery-owner"
    $evidencePath = Join-Path $directory.FullName "recovery-evidence.json"
    if (-not (Test-Path -LiteralPath $ownerPath -PathType Leaf)) { continue }
    $owner = (Get-Content -Raw -LiteralPath $ownerPath).Trim()
    if ($owner -notmatch '^recovery-[a-f0-9]{32}$') { throw "Unrecognized recovery evidence owner marker '$($directory.Name)'; refusing retention changes." }
    if (-not (Test-Path -LiteralPath $evidencePath -PathType Leaf)) { throw "Recovery evidence '$($directory.Name)' is missing its summary; refusing retention changes." }
    try { $summary = Get-Content -Raw -LiteralPath $evidencePath | ConvertFrom-Json } catch { throw "Recovery evidence '$($directory.Name)' is malformed; refusing retention changes." }
    if ([string]$summary.status -notin @('verified', 'failed')) { throw "Recovery evidence '$($directory.Name)' has an invalid status; refusing retention changes." }
    $active = (& docker ps -aq --filter "label=todir.recovery.test=$owner" 2>$null)
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace(($active -join ''))) { throw "Recovery evidence '$($directory.Name)' still has an active owned container; refusing deletion." }
    $runs.Add([pscustomobject]@{ Path = $directory.FullName; Name = $directory.Name; Owner = $owner; LastWrite = $directory.LastWriteTimeUtc; Status = [string]$summary.status })
}

$ordered = @($runs | Sort-Object LastWrite -Descending)
$deleteCandidates = @($ordered | Select-Object -Skip $RetentionCount)
foreach ($entry in $deleteCandidates) {
    $action = if ($Delete -and -not $WhatIf) { 'deleted' } else { 'would-delete' }
    if ($action -eq 'deleted') { Remove-Item -LiteralPath $entry.Path -Recurse -Force }
    Write-Output ([ordered]@{ action = $action; evidenceRoot = $entry.Path; status = $entry.Status; owner = $entry.Owner } | ConvertTo-Json -Compress)
}
