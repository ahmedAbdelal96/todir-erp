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

function Has-Property {
    param([object]$Object, [string]$Name)
    return $null -ne $Object.PSObject.Properties[$Name]
}

$entries = [Collections.Generic.List[object]]::new()
$manifestFiles = @(Get-ChildItem -LiteralPath $repository -Filter '*.manifest.json' -File)
foreach ($manifestPath in $manifestFiles) {
    try { $manifest = Get-Content -Raw -LiteralPath $manifestPath.FullName | ConvertFrom-Json } catch { throw "Malformed backup manifest '$($manifestPath.Name)'; refusing retention changes." }
    foreach ($required in @('schemaVersion', 'backupId', 'backupType', 'createdAtUtc', 'artifact', 'byteCount', 'sha256', 'status', 'repositoryMarker')) {
        if (-not (Has-Property $manifest $required)) { throw "Backup manifest '$($manifestPath.Name)' is missing '$required'; refusing retention changes." }
    }
    if ([int]$manifest.schemaVersion -ne 1) { throw "Backup manifest '$($manifestPath.Name)' has an unsupported schema version." }
    if ([string]$manifest.status -ne 'verified') { throw "Backup manifest '$($manifestPath.Name)' is not verified; refusing retention changes." }
    if ([string]$manifest.backupType -ne 'logical-custom') { throw "Manifest '$($manifestPath.Name)' is not a logical-custom backup. This helper never manages physical base backups or WAL/PITR chains." }
    if ([string]$manifest.repositoryMarker -ne '.todir-backup-repository') { throw "Backup manifest '$($manifestPath.Name)' has an invalid repository marker." }
    if ([string]$manifest.backupId -notmatch '^backup-[a-f0-9]{32}$') { throw "Backup manifest '$($manifestPath.Name)' has an invalid backup identifier." }
    $artifactName = [string]$manifest.artifact
    if ($artifactName -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,180}$') { throw "Backup manifest '$($manifestPath.Name)' has an unsafe artifact reference." }
    if ([string]$manifest.sha256 -notmatch '^[a-fA-F0-9]{64}$') { throw "Backup manifest '$($manifestPath.Name)' has an invalid SHA-256 value." }
    try {
        $createdText = if ($manifest.createdAtUtc -is [DateTime]) { $manifest.createdAtUtc.ToUniversalTime().ToString('O') } else { [string]$manifest.createdAtUtc }
        $created = [DateTimeOffset]::Parse($createdText, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind)
    } catch { throw "Backup manifest '$($manifestPath.Name)' has an invalid creation timestamp." }
    $artifact = [IO.Path]::GetFullPath((Join-Path $repository $artifactName))
    if ([IO.Path]::GetDirectoryName($artifact).TrimEnd('\', '/') -ne $repository.TrimEnd('\', '/')) { throw "Backup manifest '$($manifestPath.Name)' references a path outside the repository." }
    if (-not (Test-Path -LiteralPath $artifact -PathType Leaf)) { throw "Backup artifact '$artifactName' is missing; refusing retention changes." }
    $item = Get-Item -LiteralPath $artifact
    if ([int64]$manifest.byteCount -ne [int64]$item.Length) { throw "Backup artifact '$artifactName' has a byte-count mismatch; refusing retention changes." }
    $actualHash = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash
    if (-not $actualHash.Equals([string]$manifest.sha256, [StringComparison]::OrdinalIgnoreCase)) { throw "Backup artifact '$artifactName' has a checksum mismatch; refusing retention changes." }
    $entries.Add([pscustomobject]@{ Manifest = $manifestPath.FullName; Artifact = $artifact; BackupId = [string]$manifest.backupId; Created = $created })
}

$ordered = @($entries | Sort-Object Created -Descending)
$delete = @($ordered | Select-Object -Skip $RetentionCount)
if ($delete.Count -gt 0 -and ($ordered.Count - $delete.Count) -lt 1) { throw "Retention would remove the last valid recoverable logical backup; refusing deletion." }
foreach ($entry in $delete) {
    if (-not $WhatIf) {
        Remove-Item -LiteralPath $entry.Manifest -Force
        Remove-Item -LiteralPath $entry.Artifact -Force
    }
    Write-Output ([ordered]@{ action = if ($WhatIf) { 'would-delete' } else { 'deleted' }; backupId = $entry.BackupId; artifact = [IO.Path]::GetFileName($entry.Artifact); createdAtUtc = $entry.Created.ToString('O'); backupType = 'logical-custom' } | ConvertTo-Json -Compress)
}
