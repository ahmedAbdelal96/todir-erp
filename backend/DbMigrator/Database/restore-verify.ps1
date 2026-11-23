[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Container,
    [Parameter(Mandatory = $true)][string]$Database,
    [Parameter(Mandatory = $true)][string]$BackupPath,
    [switch]$AllowDisposableRestore,
    [string]$RestoreDatabase,
    [string]$EncryptionKeyBase64 = $env:TODIR_BACKUP_ENCRYPTION_KEY,
    [string]$VerificationSchema = "platform",
    [string]$VerificationTable = "tenant",
    [switch]$KeepDatabase,
    [string]$EvidencePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
if (-not $AllowDisposableRestore) { throw "Restore verification requires -AllowDisposableRestore. It never overwrites a source database." }
if ($Container -match '^todir-postgres(?:-\d+)?$') { throw "Refusing restore verification against the shared development PostgreSQL container." }
if ($Database -match '^(todir|todir_quality|todir_nav_acceptance)$') { throw "Refusing restore verification against a protected development database." }
$backup = [IO.Path]::GetFullPath($BackupPath)
if (-not (Test-Path -LiteralPath $backup -PathType Leaf)) { throw "Backup artifact does not exist." }
if ($VerificationSchema -notmatch '^[a-z_][a-z0-9_]*$' -or $VerificationTable -notmatch '^[a-z_][a-z0-9_]*$') { throw "Verification schema and table must be safe PostgreSQL identifiers." }
$manifestPath = "$backup.manifest.json"
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "Backup manifest is required." }
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
if ($manifest.status -ne "verified") { throw "Only a verified backup artifact can be restored." }
$actualHash = (Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne $manifest.sha256) { throw "Backup checksum mismatch; refusing restore." }

function Invoke-DockerChecked {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    $output = & docker @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Docker command failed (exit $LASTEXITCODE): docker $($Arguments -join ' ')`n$($output -join [Environment]::NewLine)" }
    return @($output)
}

function Get-EncryptionKey {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    try { $key = [Convert]::FromBase64String($Value) } catch { throw "Encryption key must be base64." }
    if ($key.Length -ne 32) { throw "Encryption key must decode to exactly 32 bytes." }
    return $key
}

function Unprotect-BackupBytes {
    param([byte[]]$Payload, [byte[]]$Key)
    $header = [Text.Encoding]::ASCII.GetBytes("TODIR-BACKUP-AESGCM-1`n")
    $headerMatches = $Payload.Length -ge ($header.Length + 12 + 16)
    if ($headerMatches) { for ($index = 0; $index -lt $header.Length; $index++) { if ($Payload[$index] -ne $header[$index]) { $headerMatches = $false; break } } }
    if (-not $headerMatches) { throw "Encrypted backup header is invalid." }
    $nonce = $Payload[$header.Length..($header.Length + 11)]; $tag = $Payload[($header.Length + 12)..($header.Length + 27)]; $cipher = $Payload[($header.Length + 28)..($Payload.Length - 1)]
    $plain = [byte[]]::new($cipher.Length); $aes = [Security.Cryptography.AesGcm]::new($Key, 16)
    try { $aes.Decrypt($nonce, $cipher, $tag, $plain) } finally { $aes.Dispose() }
    return $plain
}

$safeName = if ([string]::IsNullOrWhiteSpace($RestoreDatabase)) { "recovery_verify_$([Guid]::NewGuid().ToString('N'))" } else { $RestoreDatabase }
if ($safeName -notmatch '^recovery_verify_[a-f0-9]{8,40}$') { throw "RestoreDatabase must use the recovery_verify_<unique> safe name." }
$remotePath = "/tmp/$safeName.dump"; $decryptedPath = Join-Path ([IO.Path]::GetTempPath()) "$safeName.dump"; $created = $false; $started = [DateTimeOffset]::UtcNow
try {
    $payload = [IO.File]::ReadAllBytes($backup)
    $plain = if ($manifest.encrypted) { $key = Get-EncryptionKey $EncryptionKeyBase64; if ($null -eq $key) { throw "An encryption key is required for this artifact." }; Unprotect-BackupBytes $payload $key } else { $payload }
    [IO.File]::WriteAllBytes($decryptedPath, $plain)
    Invoke-DockerChecked @("exec", $Container, "dropdb", "--if-exists", "--force", "--username=postgres", $safeName) | Out-Null
    Invoke-DockerChecked @("exec", $Container, "createdb", "--username=postgres", $safeName) | Out-Null; $created = $true
    Invoke-DockerChecked @("cp", $decryptedPath, "${Container}:$remotePath") | Out-Null
    Invoke-DockerChecked @("exec", $Container, "pg_restore", "--username=postgres", "--exit-on-error", "--no-owner", "--no-privileges", "--dbname=$safeName", $remotePath) | Out-Null
    $tenantCount = [int64]((Invoke-DockerChecked @("exec", $Container, "psql", "--username=postgres", "--dbname=$safeName", "--tuples-only", "--no-align", "--command=select count(*) from `"$VerificationSchema`".`"$VerificationTable`"")) -join "").Trim()
    $forcedRls = [int64]((Invoke-DockerChecked @("exec", $Container, "psql", "--username=postgres", "--dbname=$safeName", "--tuples-only", "--no-align", "--command=select count(*) from pg_class c join pg_namespace n on n.oid=c.relnamespace where c.relforcerowsecurity and n.nspname <> 'pg_catalog'")) -join "").Trim()
    if ($tenantCount -lt 1 -or $forcedRls -lt 1) { throw "Restored database verification failed: tenant or FORCE RLS evidence is missing." }
    $evidence = [ordered]@{ status = "verified"; backupId = $manifest.backupId; database = $Database; restoreDatabase = $safeName; tenantCount = $tenantCount; forcedRlsTables = $forcedRls; sha256 = $actualHash; durationSeconds = ([DateTimeOffset]::UtcNow - $started).TotalSeconds; verifiedAtUtc = [DateTimeOffset]::UtcNow.ToString("O") }
    if ($EvidencePath) { $evidence | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath ([IO.Path]::GetFullPath($EvidencePath)) -Encoding UTF8 }
    Write-Output ($evidence | ConvertTo-Json -Depth 4)
}
finally {
    try { Invoke-DockerChecked @("exec", $Container, "rm", "-f", $remotePath) | Out-Null } catch { }
    if ($created -and -not $KeepDatabase) { Invoke-DockerChecked @("exec", $Container, "dropdb", "--if-exists", "--force", "--username=postgres", $safeName) | Out-Null }
    if (Test-Path -LiteralPath $decryptedPath) { Remove-Item -LiteralPath $decryptedPath -Force -ErrorAction SilentlyContinue }
}
