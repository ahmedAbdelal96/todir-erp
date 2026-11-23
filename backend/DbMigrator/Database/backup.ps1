[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Container,
    [Parameter(Mandatory = $true)][string]$Database,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [string]$EncryptionKeyBase64 = $env:TODIR_BACKUP_ENCRYPTION_KEY
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-DockerChecked {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    $output = & docker @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Docker command failed (exit $LASTEXITCODE): docker $($Arguments -join ' ')`n$($output -join [Environment]::NewLine)" }
    return @($output)
}

function Get-EncryptionKey {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    try { $key = [Convert]::FromBase64String($Value) } catch { throw "TODIR_BACKUP_ENCRYPTION_KEY must be base64." }
    if ($key.Length -ne 32) { throw "TODIR_BACKUP_ENCRYPTION_KEY must decode to exactly 32 bytes." }
    return $key
}

function Protect-BackupBytes {
    param([byte[]]$Plaintext, [byte[]]$Key)
    $nonce = [byte[]]::new(12); [Security.Cryptography.RandomNumberGenerator]::Fill($nonce)
    $tag = [byte[]]::new(16); $ciphertext = [byte[]]::new($Plaintext.Length)
    $aes = [Security.Cryptography.AesGcm]::new($Key, 16)
    try { $aes.Encrypt($nonce, $Plaintext, $ciphertext, $tag) } finally { $aes.Dispose() }
    $header = [Text.Encoding]::ASCII.GetBytes("TODIR-BACKUP-AESGCM-1`n")
    $result = [Collections.Generic.List[byte]]::new(); $result.AddRange($header); $result.AddRange($nonce); $result.AddRange($tag); $result.AddRange($ciphertext)
    return $result.ToArray()
}

$resolvedOutput = [IO.Path]::GetFullPath($OutputPath)
if ([IO.Path]::GetFileName($resolvedOutput) -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,180}$') { throw "OutputPath must name a single backup artifact." }
if (Test-Path -LiteralPath $resolvedOutput -PathType Container) { throw "OutputPath cannot be a directory." }
$parent = Split-Path -Parent $resolvedOutput
if ([string]::IsNullOrWhiteSpace($parent)) { throw "OutputPath must be absolute." }
New-Item -ItemType Directory -Force -Path $parent | Out-Null
$repositoryMarker = Join-Path $parent ".todir-backup-repository"
if (-not (Test-Path -LiteralPath $repositoryMarker)) { Set-Content -LiteralPath $repositoryMarker -Value "TODIR backup repository marker; do not delete manually.`n" -NoNewline }
if (Test-Path -LiteralPath "$resolvedOutput.manifest.json") { throw "A manifest already exists for OutputPath; refusing to overwrite a verified artifact." }

$backupId = "backup-$([Guid]::NewGuid().ToString('N'))"; $remotePath = "/tmp/$backupId.dump"
$stagingDirectory = Join-Path ([IO.Path]::GetTempPath()) $backupId; $stagingPath = Join-Path $stagingDirectory "backup.dump"
$key = Get-EncryptionKey $EncryptionKeyBase64; $encrypted = $null -ne $key; $createdAt = [DateTimeOffset]::UtcNow; $status = "failed"
try {
    New-Item -ItemType Directory -Force -Path $stagingDirectory | Out-Null
    $version = (Invoke-DockerChecked @("exec", $Container, "pg_dump", "--version")) -join " "
    Invoke-DockerChecked @("exec", $Container, "pg_dump", "--username=postgres", "--format=custom", "--no-owner", "--no-privileges", "--file=$remotePath", $Database) | Out-Null
    $restoreList = Invoke-DockerChecked @("exec", $Container, "pg_restore", "--list", $remotePath)
    if ($restoreList.Count -lt 1) { throw "pg_restore produced an empty backup catalog." }
    Invoke-DockerChecked @("cp", "${Container}:$remotePath", $stagingPath) | Out-Null
    $plainBytes = [IO.File]::ReadAllBytes($stagingPath)
    if ($plainBytes.Length -lt 128) { throw "The backup artifact is unexpectedly small." }
    $artifactBytes = if ($encrypted) { Protect-BackupBytes $plainBytes $key } else { $plainBytes }
    [IO.File]::WriteAllBytes($resolvedOutput, $artifactBytes); Remove-Item -LiteralPath $stagingPath -Force
    $hash = (Get-FileHash -LiteralPath $resolvedOutput -Algorithm SHA256).Hash.ToLowerInvariant(); $status = "verified"
    $manifest = [ordered]@{
        schemaVersion = 1; backupId = $backupId; backupType = "logical-custom"; database = $Database; sourceContainer = $Container
        postgresVersion = $version.Trim(); createdAtUtc = $createdAt.ToString("O"); artifact = [IO.Path]::GetFileName($resolvedOutput)
        byteCount = (Get-Item -LiteralPath $resolvedOutput).Length; sha256 = $hash; encrypted = $encrypted; encryption = if ($encrypted) { "AES-256-GCM" } else { "none" }
        verification = "pg_restore --list completed before artifact commit"; status = $status; repositoryMarker = [IO.Path]::GetFileName($repositoryMarker)
    }
    $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$resolvedOutput.manifest.json" -Encoding UTF8
    Write-Output ($manifest | ConvertTo-Json -Depth 4)
}
finally {
    try { Invoke-DockerChecked @("exec", $Container, "rm", "-f", $remotePath) | Out-Null } catch { }
    if (Test-Path -LiteralPath $stagingDirectory) { Remove-Item -LiteralPath $stagingDirectory -Recurse -Force -ErrorAction SilentlyContinue }
    if ($status -ne "verified" -and (Test-Path -LiteralPath $resolvedOutput)) { Remove-Item -LiteralPath $resolvedOutput -Force -ErrorAction SilentlyContinue }
}
