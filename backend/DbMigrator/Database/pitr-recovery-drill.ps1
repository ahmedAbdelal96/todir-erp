[CmdletBinding()]
param(
    [string]$EvidenceRoot = (Join-Path (Get-Location) "artifacts/recovery"),
    [string]$PostgresImage = "postgres:18",
    [switch]$SkipApplicationMigration,
    [switch]$KeepEnvironment
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $PSCommandPath
$repositoryRoot = (Resolve-Path (Join-Path $scriptRoot "../../..")).Path
$evidence = [IO.Path]::GetFullPath($EvidenceRoot)
$allowedEvidencePrefix = [IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts"))
if ($evidence -eq $allowedEvidencePrefix -or -not $evidence.StartsWith($allowedEvidencePrefix + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "EvidenceRoot must be a child of the ignored repository artifacts directory." }
if (Test-Path -LiteralPath $evidence) { throw "EvidenceRoot already exists; refusing to reuse recovery evidence or resources." }
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
$ownership = Join-Path $evidence ".todir-recovery-owner"
$testId = "recovery-$([Guid]::NewGuid().ToString('N'))"
Set-Content -LiteralPath $ownership -Value $testId -NoNewline
$storageRoot = Join-Path ([IO.Path]::GetTempPath()) $testId
$storageOwnership = Join-Path $storageRoot ".todir-recovery-storage-owner"
New-Item -ItemType Directory -Force -Path $storageRoot | Out-Null
Set-Content -LiteralPath $storageOwnership -Value $testId -NoNewline
$testStarted = [DateTimeOffset]::UtcNow
$names = [Collections.Generic.List[string]]::new()
$origin = "todir-$testId-origin"; $target = "todir-$testId-target"; $latestTarget = "todir-$testId-latest"; $brokenTarget = "todir-$testId-broken"
foreach ($name in @($origin, $target, $latestTarget, $brokenTarget)) { $names.Add([string]$name) }
$originData = Join-Path $storageRoot "origin-data"; $baseData = Join-Path $storageRoot "base-backup"; $targetData = Join-Path $storageRoot "target-data"; $latestData = Join-Path $storageRoot "latest-data"; $brokenData = Join-Path $storageRoot "broken-data"; $corruptData = Join-Path $storageRoot "corrupt-data"
$archive = Join-Path $storageRoot "wal"; $brokenArchive = Join-Path $storageRoot "broken-wal"; $corruptArchive = Join-Path $storageRoot "corrupt-wal"; $backupRepository = Join-Path $storageRoot "logical-backups"
foreach ($path in @($originData, $baseData, $targetData, $latestData, $brokenData, $corruptData, $archive, $brokenArchive, $corruptArchive, $backupRepository)) { New-Item -ItemType Directory -Force -Path $path | Out-Null }
$port = 18000 + (Get-Random -Minimum 100 -Maximum 700); $targetPort = $port + 1; $latestPort = $port + 2; $brokenPort = $port + 3; $corruptPort = $port + 4; $apiPort = $port + 5
$targetMarker = "pitr-target-$($testId.Substring($testId.Length - 12))"
$tenantA = "11111111-1111-1111-1111-111111111111"; $tenantB = "22222222-2222-2222-2222-222222222222"
$keyBytes = [byte[]]::new(32); [Security.Cryptography.RandomNumberGenerator]::Fill($keyBytes); $key = [Convert]::ToBase64String($keyBytes)
$wrongKeyBytes = [byte[]]::new(32); [Security.Cryptography.RandomNumberGenerator]::Fill($wrongKeyBytes); $wrongKey = [Convert]::ToBase64String($wrongKeyBytes)
$previousBackupKey = $env:TODIR_BACKUP_ENCRYPTION_KEY
$results = [ordered]@{ testId = $testId; commit = ((git -C $repositoryRoot rev-parse HEAD).Trim()); postgresImage = $PostgresImage; postgresVersion = $null; backupId = $null; recoveryTarget = $null; status = "failed"; checks = [ordered]@{}; environment = [ordered]@{ origin = $origin; target = $target; evidenceRoot = $evidence } }
$failure = $null

function Invoke-DockerChecked {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    $output = & docker @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Docker command failed (exit $LASTEXITCODE): docker $($Arguments -join ' ')`n$($output -join [Environment]::NewLine)" }
    return @($output)
}

function Invoke-Sql {
    param([string]$Container, [string]$Sql, [string]$Database = "postgres")
    return ((Invoke-DockerChecked @("exec", $Container, "psql", "--username=postgres", "--dbname=$Database", "--tuples-only", "--no-align", "--set=ON_ERROR_STOP=1", "--command=$Sql")) -join "`n").Trim()
}

function Invoke-ScriptChecked {
    param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)][string[]]$Arguments)
    $output = & pwsh -NoProfile -File $Path @Arguments 2>&1
    if ($output) { $output | Out-Host }
    if ($LASTEXITCODE -ne 0) { throw "Recovery helper failed with exit code $($LASTEXITCODE): $Path" }
}

function Wait-Postgres {
    param([string]$Container, [int]$Seconds = 45)
    $consecutiveReady = 0
    for ($i = 0; $i -lt ($Seconds * 2); $i++) {
        try {
            Invoke-DockerChecked @("exec", $Container, "pg_isready", "--username=postgres", "--dbname=postgres") | Out-Null
            Invoke-DockerChecked @("exec", $Container, "psql", "--username=postgres", "--dbname=postgres", "--command=select 1") | Out-Null
            $consecutiveReady++
            if ($consecutiveReady -ge 2) { return }
        }
        catch { $consecutiveReady = 0 }
        Start-Sleep -Milliseconds 500
    }
    throw "PostgreSQL container did not become ready: $Container"
}

function Start-Postgres {
    param([string]$Name, [string]$Data, [string]$Wal, [int]$HostPort, [string]$Base, [switch]$Recovery)
    $args = @("run", "-d", "--name", $Name, "--label", "todir.recovery.test=$testId", "--label", "todir.recovery.disposable=true", "-e", "POSTGRES_PASSWORD=postgres", "-e", "POSTGRES_DB=postgres", "-p", "${HostPort}:5432", "--mount", "type=bind,source=$Data,target=/var/lib/postgresql", "--mount", "type=bind,source=$Wal,target=/wal")
    if (-not [string]::IsNullOrWhiteSpace($Base)) { $args += @("--mount", "type=bind,source=$Base,target=/base") }
    $args += @($PostgresImage, "-c", "listen_addresses=*", "-c", "max_connections=100")
    if (-not $Recovery) { $args += @("-c", "wal_level=replica", "-c", "archive_mode=on", "-c", "archive_command=test ! -f /wal/%f && dd if=%p of=/wal/%f bs=16M conv=sync") }
    Invoke-DockerChecked $args | Out-Null
    Wait-Postgres $Name
}

function Stop-Disposable {
    param([string]$Name)
    try {
        $label = (& docker inspect --format '{{ index .Config.Labels "todir.recovery.test" }}' $Name 2>$null).Trim()
        if ($label -eq $testId) { & docker rm --force $Name 2>$null | Out-Null }
    } catch { }
}

function Copy-Tree {
    param([string]$Source, [string]$Destination)
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    foreach ($item in Get-ChildItem -LiteralPath $Source -Force) { Copy-Item -LiteralPath $item.FullName -Destination $Destination -Recurse -Force }
}

function Copy-BaseData {
    param([string]$Base, [string]$DestinationRoot)
    $destination = Join-Path $DestinationRoot "18/docker"
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    foreach ($item in Get-ChildItem -LiteralPath $Base -Force) { Copy-Item -LiteralPath $item.FullName -Destination $destination -Recurse -Force }
    return $destination
}

function Configure-RecoveryTarget {
    param([string]$Data, [string]$TargetLine)
    New-Item -ItemType File -Force -Path (Join-Path $Data "recovery.signal") | Out-Null
    @("restore_command = 'cp /wal/%f %p'", $TargetLine, "recovery_target_action = 'promote'", "recovery_target_timeline = 'latest'") | Set-Content -LiteralPath (Join-Path $Data "postgresql.auto.conf") -Encoding ASCII
}

function Start-RecoveryTarget {
    param([string]$Name, [string]$Data, [string]$Wal, [int]$HostPort)
    $args = @("run", "-d", "--name", $Name, "--label", "todir.recovery.test=$testId", "--label", "todir.recovery.disposable=true", "-p", "${HostPort}:5432", "--mount", "type=bind,source=$Data,target=/var/lib/postgresql", "--mount", "type=bind,source=$Wal,target=/wal", $PostgresImage, "-c", "listen_addresses=*")
    Invoke-DockerChecked $args | Out-Null
}

function Wait-Archive {
    param([string]$Path, [int]$Minimum = 1, [int]$Seconds = 30)
    for ($i = 0; $i -lt ($Seconds * 2); $i++) { if ((@(Get-ChildItem -LiteralPath $Path -File -ErrorAction SilentlyContinue)).Count -ge $Minimum) { return }; Start-Sleep -Milliseconds 500 }
    throw "WAL archive did not reach the expected minimum file count."
}

function Assert-RecoveryReady {
    param([string]$Container, [int]$Seconds = 180)
    Wait-Postgres $Container $Seconds
    $baseline = Invoke-Sql $Container "select count(*) from recovery_test.recovery_probe where phase='baseline'"
    $target = Invoke-Sql $Container "select count(*) from recovery_test.recovery_probe where phase='target'"
    $post = Invoke-Sql $Container "select count(*) from recovery_test.recovery_probe where phase='post-target'"
    if ($baseline -ne '1' -or $target -ne '1') { throw "Recovered database is missing baseline or target commit." }
    return [ordered]@{ baseline = [int]$baseline; target = [int]$target; postTarget = [int]$post }
}

function Invoke-ApiHealthSmoke {
    param([int]$DatabasePort, [int]$HttpPort)
    $stdout = Join-Path $storageRoot "api-smoke.stdout.log"; $stderr = Join-Path $storageRoot "api-smoke.stderr.log"
    $saved = @{}
    foreach ($name in @("ASPNETCORE_ENVIRONMENT", "ASPNETCORE_URLS", "TODIR_DATABASE_CONNECTION", "TODIR_RABBITMQ_HOST")) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
    $process = $null
    try {
        $env:ASPNETCORE_ENVIRONMENT = "Development"
        $env:ASPNETCORE_URLS = "http://127.0.0.1:$HttpPort"
        $env:TODIR_DATABASE_CONNECTION = "Host=127.0.0.1;Port=$DatabasePort;Database=postgres;Username=erp_app;Password=recovery-app-only"
        $env:TODIR_RABBITMQ_HOST = ""
        $process = Start-Process -FilePath "dotnet" -WorkingDirectory $repositoryRoot -ArgumentList @((Join-Path $repositoryRoot "backend/Api/bin/Release/net10.0/Api.dll")) -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
        $ready = $false
        for ($attempt = 0; $attempt -lt 90; $attempt++) {
            if ($process.HasExited) { throw "API exited before health readiness; inspect $stderr." }
            try { $response = Invoke-WebRequest -Uri "http://127.0.0.1:$HttpPort/health/ready" -UseBasicParsing -TimeoutSec 2; if ($response.StatusCode -eq 200) { $ready = $true; break } } catch { }
            Start-Sleep -Seconds 1
        }
        if (-not $ready) { throw "Restored API did not reach /health/ready within the bounded smoke window." }
    }
    finally {
        if ($null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
        foreach ($entry in $saved.GetEnumerator()) { if ($null -eq $entry.Value) { Remove-Item "Env:$($entry.Key)" -ErrorAction SilentlyContinue } else { Set-Item "Env:$($entry.Key)" $entry.Value } }
    }
}

try {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw "Docker is required for the isolated recovery drill." }
    Start-Postgres $origin $originData $archive $port $baseData
    if (-not $SkipApplicationMigration) {
        $previousMigratorConnection = $env:TODIR_MIGRATOR_CONNECTION; $previousRepositoryRoot = $env:TODIR_REPOSITORY_ROOT; $previousRuntimePassword = $env:TODIR_RUNTIME_PASSWORD; $previousDotnetEnvironment = $env:DOTNET_ENVIRONMENT
        try {
            $env:TODIR_MIGRATOR_CONNECTION = "Host=127.0.0.1;Port=$port;Database=postgres;Username=postgres;Password=postgres"
            $env:TODIR_REPOSITORY_ROOT = $repositoryRoot
            $env:TODIR_RUNTIME_PASSWORD = "recovery-app-only"
            $env:DOTNET_ENVIRONMENT = "Development"
            $migrationOutput = & dotnet run --project (Join-Path $repositoryRoot "backend/DbMigrator/DbMigrator.csproj") --configuration Release --no-restore --no-build 2>&1
            $migrationOutput | Out-Host
            if ($LASTEXITCODE -ne 0) { throw "Isolated database migration failed with exit code $LASTEXITCODE." }
        }
        finally {
            if ($null -eq $previousMigratorConnection) { Remove-Item Env:TODIR_MIGRATOR_CONNECTION -ErrorAction SilentlyContinue } else { $env:TODIR_MIGRATOR_CONNECTION = $previousMigratorConnection }
            if ($null -eq $previousRepositoryRoot) { Remove-Item Env:TODIR_REPOSITORY_ROOT -ErrorAction SilentlyContinue } else { $env:TODIR_REPOSITORY_ROOT = $previousRepositoryRoot }
            if ($null -eq $previousRuntimePassword) { Remove-Item Env:TODIR_RUNTIME_PASSWORD -ErrorAction SilentlyContinue } else { $env:TODIR_RUNTIME_PASSWORD = $previousRuntimePassword }
            if ($null -eq $previousDotnetEnvironment) { Remove-Item Env:DOTNET_ENVIRONMENT -ErrorAction SilentlyContinue } else { $env:DOTNET_ENVIRONMENT = $previousDotnetEnvironment }
        }
    }
    $probeBootstrap = if ($SkipApplicationMigration) { "create role erp_app login password 'recovery-app-only' nosuperuser nocreatedb nocreaterole noreplication nobypassrls;" } else { "" }
    Invoke-Sql $origin "$probeBootstrap create schema recovery_test; create table recovery_test.recovery_probe(phase text primary key, committed_at timestamptz not null default clock_timestamp()); create table recovery_test.tenant_probe(tenant_id uuid primary key, value text not null); alter table recovery_test.tenant_probe enable row level security; alter table recovery_test.tenant_probe force row level security; create policy tenant_probe_isolation on recovery_test.tenant_probe using (tenant_id = nullif(current_setting('app.tenant_id', true),'')::uuid); grant usage on schema recovery_test to erp_app; grant select on recovery_test.tenant_probe to erp_app; insert into recovery_test.tenant_probe values ('$tenantA','A'),('$tenantB','B'); insert into recovery_test.recovery_probe(phase) values ('baseline');" | Out-Null
    $backupPath = Join-Path $backupRepository "erp-logical.dump"
    $env:TODIR_BACKUP_ENCRYPTION_KEY = $key
    Invoke-ScriptChecked (Join-Path $scriptRoot "backup.ps1") @("-Container", $origin, "-Database", "postgres", "-OutputPath", $backupPath, "-RequireEncryption")
    if (-not (Test-Path -LiteralPath "$backupPath.manifest.json")) { throw "Logical backup manifest was not created." }
    $backupManifest = Get-Content -Raw -LiteralPath "$backupPath.manifest.json" | ConvertFrom-Json
    $results.backupId = [string]$backupManifest.backupId
    $results.postgresVersion = [string]$backupManifest.postgresVersion
    $results.checks.logicalBackup = "PASS"
    Invoke-ScriptChecked (Join-Path $scriptRoot "restore-verify.ps1") @("-Container", $origin, "-Database", "postgres", "-BackupPath", $backupPath, "-AllowDisposableRestore", "-VerificationSchema", "recovery_test", "-VerificationTable", "recovery_probe", "-EvidencePath", (Join-Path $evidence "logical-restore.json"))
    $results.checks.logicalRestore = "PASS"
    try { $env:TODIR_BACKUP_ENCRYPTION_KEY = $wrongKey; Invoke-ScriptChecked (Join-Path $scriptRoot "restore-verify.ps1") @("-Container", $origin, "-Database", "postgres", "-BackupPath", $backupPath, "-AllowDisposableRestore"); throw "Wrong-key restore unexpectedly succeeded." } catch { if ($_.Exception.Message -match 'Wrong-key restore unexpectedly') { throw }; $results.checks.wrongKeyRejected = "PASS" } finally { $env:TODIR_BACKUP_ENCRYPTION_KEY = $key }
    try { Invoke-ScriptChecked (Join-Path $scriptRoot "restore-verify.ps1") @("-Container", $origin, "-Database", "postgres", "-BackupPath", $backupPath); throw "Unsafe restore unexpectedly succeeded." } catch { if ($_.Exception.Message -match 'Unsafe restore unexpectedly') { throw }; $results.checks.unsafeRestoreRejected = "PASS" }
    try { Invoke-ScriptChecked (Join-Path $scriptRoot "backup.ps1") @("-Container", $origin, "-Database", "missing_database", "-OutputPath", (Join-Path $backupRepository "failed.dump")); throw "Failed backup unexpectedly succeeded." } catch { if ($_.Exception.Message -match 'Failed backup unexpectedly') { throw }; $results.checks.failedBackupDetected = "PASS" }

    Invoke-DockerChecked @("exec", $origin, "pg_basebackup", "--username=postgres", "--pgdata=/base", "--format=plain", "--wal-method=stream", "--progress") | Out-Null
    Get-ChildItem -LiteralPath $archive -File -ErrorAction SilentlyContinue | Remove-Item -Force
    Invoke-Sql $origin "insert into recovery_test.recovery_probe(phase) values ('target');" | Out-Null
    Invoke-Sql $origin "select pg_create_restore_point('$targetMarker');" | Out-Null
    Invoke-Sql $origin "insert into recovery_test.recovery_probe(phase) values ('post-target');" | Out-Null
    $latestLsn = Invoke-Sql $origin "select pg_current_wal_lsn();"
    Invoke-Sql $origin "create table recovery_test.wal_padding(value text not null); insert into recovery_test.wal_padding select repeat('x',8000) from generate_series(1,5000);" | Out-Null
    Invoke-Sql $origin "select pg_switch_wal();" | Out-Null
    Invoke-Sql $origin "select pg_switch_wal();" | Out-Null
    Wait-Archive $archive
    $results.walTarget = [ordered]@{ restorePoint = $targetMarker; latestLsn = $latestLsn; archiveFiles = @((Get-ChildItem -LiteralPath $archive -File)).Count }
    $results.recoveryTarget = $targetMarker
    Stop-Disposable $origin

    $targetPgData = Copy-BaseData $baseData $targetData; Configure-RecoveryTarget $targetPgData "recovery_target_name = '$targetMarker'"; Start-RecoveryTarget $target $targetData $archive $targetPort
    $targetValues = Assert-RecoveryReady $target
    $results.checks.pitr = "PASS"; $results.pitr = $targetValues
    Stop-Disposable $target; Remove-Item -LiteralPath $targetData -Recurse -Force; New-Item -ItemType Directory -Force -Path $targetData | Out-Null

    $latestPgData = Copy-BaseData $baseData $latestData; Configure-RecoveryTarget $latestPgData "recovery_target_lsn = '$latestLsn'"; Start-RecoveryTarget $latestTarget $latestData $archive $latestPort
    Wait-Postgres $latestTarget 180; if ((Invoke-Sql $latestTarget "select count(*) from recovery_test.recovery_probe where phase='post-target'") -ne '1') { throw "Latest archived WAL recovery did not contain the latest commit." }
    $results.checks.latestArchivedPoint = "PASS"

    $runtimeCheck = ((Invoke-Sql $latestTarget "set role erp_app; select set_config('app.tenant_id','$tenantA',false); select count(*) from recovery_test.tenant_probe where tenant_id='$tenantB';") -split "`n" | Select-Object -Last 1).Trim()
    if ($runtimeCheck -ne '0') { throw "Runtime role crossed the tenant RLS boundary after restore." }
    $rlsEvidence = Invoke-Sql $latestTarget "select count(*) from pg_class c join pg_namespace n on n.oid=c.relnamespace where c.relforcerowsecurity and n.nspname='recovery_test';"
    if ($rlsEvidence -ne '1') { throw "FORCE RLS was not restored." }
    $results.checks.runtimeRoleRls = "PASS"
    Invoke-ApiHealthSmoke $latestPort $apiPort
    $results.checks.restoredApiHealth = "PASS"
    Stop-Disposable $latestTarget; Remove-Item -LiteralPath $latestData -Recurse -Force; New-Item -ItemType Directory -Force -Path $latestData | Out-Null

    $brokenPgData = Copy-BaseData $baseData $brokenData; New-Item -ItemType Directory -Force -Path $brokenArchive | Out-Null
    Configure-RecoveryTarget $brokenPgData "recovery_target_name = '$targetMarker'"
    Start-RecoveryTarget $brokenTarget $brokenData $brokenArchive $brokenPort; Start-Sleep -Seconds 8
    $brokenLog = ((& docker logs $brokenTarget 2>&1) -join "`n"); $brokenRunning = (& docker inspect --format '{{.State.Running}}' $brokenTarget 2>$null); $brokenExitCode = (& docker inspect --format '{{.State.ExitCode}}' $brokenTarget 2>$null); $brokenReadyOutput = (& docker exec $brokenTarget pg_isready --username=postgres --dbname=postgres 2>&1); $brokenReadyCode = $LASTEXITCODE; if ($brokenRunning -eq 'true') { Stop-Disposable $brokenTarget }
    if ($brokenRunning -eq 'true' -and $brokenReadyCode -eq 0 -and $brokenLog -notmatch 'could not open file|requested WAL segment|waiting for WAL|could not restore file|cannot stat|archive file') { throw "Missing-WAL recovery did not produce a visible recovery failure." }
    $results.checks.missingWalRejected = "PASS"

    $corruptPgData = Copy-BaseData $baseData $corruptData; Copy-Tree $archive $corruptArchive
    $corruptWalFile = Get-ChildItem -LiteralPath $corruptArchive -File | Where-Object { $_.Length -gt 0 } | Select-Object -First 1
    if ($null -eq $corruptWalFile) { throw "No archived WAL file was available for corruption testing." }
    $corruptBytes = [IO.File]::ReadAllBytes($corruptWalFile.FullName); $corruptBytes[[Math]::Floor($corruptBytes.Length / 2)] = $corruptBytes[[Math]::Floor($corruptBytes.Length / 2)] -bxor 255; [IO.File]::WriteAllBytes($corruptWalFile.FullName, $corruptBytes)
    Configure-RecoveryTarget $corruptPgData "recovery_target_name = '$targetMarker'"
    $corruptTarget = "todir-$testId-corrupt"; $names.Add($corruptTarget); Start-RecoveryTarget $corruptTarget $corruptData $corruptArchive $corruptPort; Start-Sleep -Seconds 8
    $corruptLog = ((& docker logs $corruptTarget 2>&1) -join "`n"); $corruptRunning = (& docker inspect --format '{{.State.Running}}' $corruptTarget 2>$null); $corruptExitCode = (& docker inspect --format '{{.State.ExitCode}}' $corruptTarget 2>$null); $corruptReadyOutput = (& docker exec $corruptTarget pg_isready --username=postgres --dbname=postgres 2>&1); $corruptReadyCode = $LASTEXITCODE; if ($corruptRunning -eq 'true') { Stop-Disposable $corruptTarget }
    if ($corruptRunning -eq 'true' -and $corruptReadyCode -eq 0 -and $corruptLog -notmatch 'invalid record|PANIC|could not restore file|unexpected pageaddr|invalid resource manager ID|invalid resource manager') { throw "Corrupt-WAL recovery did not produce a visible recovery failure." }
    $results.checks.corruptWalRejected = "PASS"

    $results.durationSeconds = ([DateTimeOffset]::UtcNow - $testStarted).TotalSeconds
    $results.status = "verified"
    Write-Output ($results | ConvertTo-Json -Depth 8)
}
catch {
    $message = [string]$_.Exception.Message
    $message = $message -replace '(?i)(password|secret|token|credential|encryption key|backup key)[^\r\n;]*', '$1=[REDACTED]'
    $message = $message -replace '[A-Za-z0-9+/]{40,}={0,2}', '[REDACTED]'
    $failure = [ordered]@{ type = $_.Exception.GetType().FullName; message = $message }
    $results.failure = $failure
    throw
}
finally {
    $results.durationSeconds = ([DateTimeOffset]::UtcNow - $testStarted).TotalSeconds
    if ($results.status -eq "verified") { $results.finalAcceptance = "PASS" } else { $results.status = "failed"; $results.finalAcceptance = "FAIL" }
    if ($null -ne $failure) { $results.failure = $failure }
    if ((Test-Path -LiteralPath $evidence -PathType Container) -and (Test-Path -LiteralPath $ownership -PathType Leaf) -and ((Get-Content -Raw -LiteralPath $ownership) -eq $testId)) {
        $results | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $evidence "recovery-evidence.json") -Encoding UTF8
    }
    if ($null -eq $previousBackupKey) { Remove-Item Env:TODIR_BACKUP_ENCRYPTION_KEY -ErrorAction SilentlyContinue } else { $env:TODIR_BACKUP_ENCRYPTION_KEY = $previousBackupKey }
    if (-not $KeepEnvironment) {
        foreach ($name in $names) { Stop-Disposable $name }
        if ((Test-Path -LiteralPath $storageRoot -PathType Container) -and (Test-Path -LiteralPath $storageOwnership -PathType Leaf) -and ((Get-Content -Raw -LiteralPath $storageOwnership) -eq $testId)) { Remove-Item -LiteralPath $storageRoot -Recurse -Force -ErrorAction SilentlyContinue }
    }
}
