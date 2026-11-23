[CmdletBinding()]
param(
    [string]$EvidenceRoot = (Join-Path (Get-Location) ("artifacts/recovery-safety-" + [Guid]::NewGuid().ToString('N'))),
    [string]$PostgresImage = "postgres:18"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $PSCommandPath
$repositoryRoot = (Resolve-Path (Join-Path $scriptRoot "../../..")).Path
$evidence = [IO.Path]::GetFullPath($EvidenceRoot)
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts"))
if (-not ($evidence.StartsWith($allowedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))) { throw "EvidenceRoot must be under the ignored repository artifacts directory." }
if (Test-Path -LiteralPath $evidence) { throw "EvidenceRoot already exists; refusing to reuse safety evidence." }
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
$testId = "recovery-$([Guid]::NewGuid().ToString('N'))"
$ownerPath = Join-Path $evidence ".todir-recovery-owner"
Set-Content -LiteralPath $ownerPath -Value $testId -NoNewline
$storageRoot = Join-Path ([IO.Path]::GetTempPath()) $testId
$container = "recovery-safety-$($testId.Substring($testId.Length - 12))"
$unrecognized = "$container-unrecognized"
$port = 19000 + (Get-Random -Minimum 100 -Maximum 700)
$existingDatabase = "recovery_verify_$([Guid]::NewGuid().ToString('N'))"
$backupPath = Join-Path $storageRoot "backup.dump"
$containerCreated = $false; $unrecognizedCreated = $false; $existingCreated = $false
$keyBytes = [byte[]]::new(32); [Security.Cryptography.RandomNumberGenerator]::Fill($keyBytes); $key = [Convert]::ToBase64String($keyBytes)
$previousKey = $env:TODIR_BACKUP_ENCRYPTION_KEY
$results = [ordered]@{ testId = $testId; commit = ((git -C $repositoryRoot rev-parse HEAD).Trim()); postgresImage = $PostgresImage; postgresVersion = $null; backupId = $null; status = "failed"; checks = [ordered]@{}; failure = $null }
$started = [DateTimeOffset]::UtcNow

function Invoke-DockerChecked {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    $output = & docker @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Docker command failed (exit $LASTEXITCODE)." }
    return @($output)
}
function Invoke-Sql {
    param([string]$Sql, [string]$Database = "postgres", [string]$Target = $container)
    return ((Invoke-DockerChecked @("exec", $Target, "psql", "--username=postgres", "--dbname=$Database", "--tuples-only", "--no-align", "--set=ON_ERROR_STOP=1", "--command=$Sql")) -join "").Trim()
}
function Wait-Postgres {
    for ($i = 0; $i -lt 90; $i++) {
        try { Invoke-DockerChecked @("exec", $container, "pg_isready", "--username=postgres", "--dbname=postgres") | Out-Null; Invoke-Sql "select 1" | Out-Null; return } catch { Start-Sleep -Milliseconds 500 }
    }
    throw "Disposable PostgreSQL did not become ready."
}
function Invoke-ExpectedFailure {
    param([string]$Path, [string[]]$Arguments, [string]$ExpectedReason)
    $output = & pwsh -NoProfile -File $Path @Arguments 2>&1
    if ($LASTEXITCODE -eq 0) { throw "Expected helper failure unexpectedly succeeded: $Path" }
    $text = ((($output -join "`n") -replace "`e\[[0-?]*[ -/]*[@-~]", "") -replace '\s+', ' ')
    if ($text -notmatch $ExpectedReason) { throw "Expected failure reason '$ExpectedReason' was not observed. Actual output: $text" }
}

try {
    New-Item -ItemType Directory -Force -Path $storageRoot | Out-Null
    Invoke-DockerChecked @("run", "-d", "--name", $container, "--label", "todir.recovery.test=$testId", "--label", "todir.recovery.disposable=true", "-e", "POSTGRES_PASSWORD=postgres", "-p", "${port}:5432", $PostgresImage) | Out-Null
    $containerCreated = $true; Wait-Postgres
    $version = ((Invoke-DockerChecked @("exec", $container, "postgres", "--version")) -join " ").Trim(); $results.postgresVersion = $version
    Invoke-Sql "create schema recovery_test; create table recovery_test.recovery_probe(id integer primary key); insert into recovery_test.recovery_probe values (1); create table recovery_test.tenant_probe(id integer primary key); alter table recovery_test.tenant_probe enable row level security; alter table recovery_test.tenant_probe force row level security; insert into recovery_test.tenant_probe values (1);" | Out-Null
    $env:TODIR_BACKUP_ENCRYPTION_KEY = $key
    & pwsh -NoProfile -File (Join-Path $scriptRoot "backup.ps1") -Container $container -Database postgres -OutputPath $backupPath -RequireEncryption 2>&1 | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Safety regression backup failed." }
    $manifest = Get-Content -Raw -LiteralPath "$backupPath.manifest.json" | ConvertFrom-Json
    $results.backupId = [string]$manifest.backupId; $results.checks.encryptedBackup = "PASS"

    if ($existingDatabase -notmatch '^recovery_verify_[a-f0-9]{32}$') { throw "The existing-database fixture name is not valid for restore verification." }
    Invoke-DockerChecked @("exec", $container, "createdb", "--username=postgres", $existingDatabase) | Out-Null; $existingCreated = $true
    Invoke-Sql -Database $existingDatabase "create table public.safety_marker(value text primary key); insert into public.safety_marker values ('protected');" | Out-Null
    if ((Invoke-Sql "select exists(select 1 from pg_database where datname='$existingDatabase');") -ne 't') { throw "Existing database fixture was not present before the restore attempt." }
    Invoke-ExpectedFailure (Join-Path $scriptRoot "restore-verify.ps1") @("-Container", $container, "-Database", "postgres", "-BackupPath", $backupPath, "-AllowDisposableRestore", "-RestoreDatabase", $existingDatabase) 'already exists; refusing to overwrite'
    if ((Invoke-Sql "select exists(select 1 from pg_database where datname='$existingDatabase');") -ne 't') { throw "Existing similarly named database disappeared." }
    if ((Invoke-Sql -Database $existingDatabase "select count(*) from public.safety_marker;") -ne '1') { throw "Existing database marker changed." }
    $results.checks.existingDatabaseProtected = "PASS"

    Invoke-ExpectedFailure (Join-Path $scriptRoot "restore-verify.ps1") @("-Container", $container, "-Database", "postgres", "-BackupPath", $backupPath, "-AllowDisposableRestore", "-RestoreDatabase", "postgres") 'RestoreDatabase must use the recovery_verify_<unique> safe name'
    $results.checks.protectedDatabaseRejected = "PASS"

    Invoke-DockerChecked @("run", "-d", "--name", $unrecognized, "-e", "POSTGRES_PASSWORD=postgres", $PostgresImage) | Out-Null; $unrecognizedCreated = $true
    Invoke-ExpectedFailure (Join-Path $scriptRoot "restore-verify.ps1") @("-Container", $unrecognized, "-Database", "postgres", "-BackupPath", $backupPath, "-AllowDisposableRestore") 'Refusing restore verification unless'
    $results.checks.unrecognizedContainerRejected = "PASS"

    $tampered = Join-Path $storageRoot "tampered.dump"; Copy-Item -LiteralPath $backupPath -Destination $tampered
    $tamperedBytes = [IO.File]::ReadAllBytes($tampered); $tamperedBytes[($tamperedBytes.Length - 1)] = $tamperedBytes[($tamperedBytes.Length - 1)] -bxor 255; [IO.File]::WriteAllBytes($tampered, $tamperedBytes)
    Copy-Item -LiteralPath "$backupPath.manifest.json" -Destination "$tampered.manifest.json"
    Invoke-ExpectedFailure (Join-Path $scriptRoot "restore-verify.ps1") @("-Container", $container, "-Database", "postgres", "-BackupPath", $tampered, "-AllowDisposableRestore") 'checksum mismatch'
    $results.checks.checksumMismatchRejected = "PASS"

    & pwsh -NoProfile -File (Join-Path $scriptRoot "restore-verify.ps1") -Container $container -Database postgres -BackupPath $backupPath -AllowDisposableRestore -VerificationSchema recovery_test -VerificationTable recovery_probe 2>&1 | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "A newly created disposable restore target did not complete." }
    if ((Invoke-Sql "select exists(select 1 from pg_database where datname like 'recovery_verify_%' and datname <> '$existingDatabase');") -ne 'f') { throw "A restore target created by this run was not cleaned." }
    $results.checks.createdTargetCleaned = "PASS"
    $results.status = "verified"
}
catch {
    $message = [string]$_.Exception.Message
    $message = $message -replace '(?i)(password|secret|token|credential|encryption key|backup key)[^\r\n;]*', '$1=[REDACTED]'
    $message = $message -replace '[A-Za-z0-9+/]{40,}={0,2}', '[REDACTED]'
    $results.failure = [ordered]@{ type = $_.Exception.GetType().FullName; message = $message }
    throw
}
finally {
    $results.durationSeconds = ([DateTimeOffset]::UtcNow - $started).TotalSeconds
    $results.finalAcceptance = if ($results.status -eq 'verified') { 'PASS' } else { 'FAIL' }
    if ($results.status -eq 'failed' -and $null -eq $results.failure) { $results.failure = [ordered]@{ type = 'Unknown'; message = 'Safety regression failed before a sanitized exception was captured.' } }
    $results | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $evidence "recovery-evidence.json") -Encoding UTF8
    if ($null -eq $previousKey) { Remove-Item Env:TODIR_BACKUP_ENCRYPTION_KEY -ErrorAction SilentlyContinue } else { $env:TODIR_BACKUP_ENCRYPTION_KEY = $previousKey }
    if ($unrecognizedCreated) { & docker rm --force $unrecognized 2>$null | Out-Null }
    if ($existingCreated -and $containerCreated) { try { Invoke-DockerChecked @("exec", $container, "dropdb", "--username=postgres", $existingDatabase) | Out-Null } catch { } }
    if ($containerCreated) { & docker rm --force $container 2>$null | Out-Null }
    if (Test-Path -LiteralPath $storageRoot) { Remove-Item -LiteralPath $storageRoot -Recurse -Force -ErrorAction SilentlyContinue }
}
