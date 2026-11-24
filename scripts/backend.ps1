$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$settingsPath = Join-Path $repositoryRoot 'backend/appsettings.Development.json'
$databaseName = 'todir'
$hostProcesses = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()

function Get-ConnectionValue {
    param(
        [Parameter(Mandatory = $true)][string]$ConnectionString,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $pattern = '(?i)(?:^|;)\s*' + [regex]::Escape($Name) + '\s*=\s*(?:"([^"]*(?:""[^"]*)*)"|''([^'']*(?:''''[^'']*)*)''|([^;]*))'
    $match = [regex]::Match($ConnectionString, $pattern)
    if (-not $match.Success) { return $null }
    foreach ($groupNumber in 1..3) {
        if ($match.Groups[$groupNumber].Success) { return $match.Groups[$groupNumber].Value.Trim() }
    }
    return $null
}

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)][string]$Command,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$Description
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Description failed with exit code $LASTEXITCODE." }
}

function Get-RequiredSecret {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [int]$MinimumLength = 16
    )

    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Set $Name in the local process environment before starting the development services."
    }
    if ($value.Length -lt $MinimumLength) {
        throw "$Name must contain at least $MinimumLength characters."
    }
    return $value
}

function Stop-ProcessTree {
    param([System.Diagnostics.Process]$Process)

    if ($Process.HasExited) { return }
    & taskkill.exe /PID $Process.Id /T /F *> $null
}

function Stop-ExistingLocalHosts {
    $apiProject = Join-Path $repositoryRoot 'backend\Api\Api.csproj'
    $workerProject = Join-Path $repositoryRoot 'backend\Worker\Worker.csproj'
    $apiOutput = (Join-Path $repositoryRoot 'backend\Api\bin') + '\'
    $workerOutput = (Join-Path $repositoryRoot 'backend\Worker\bin') + '\'
    $existingHosts = Get-CimInstance Win32_Process | Where-Object {
        ($_.CommandLine -and ($_.CommandLine.IndexOf($apiProject, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or $_.CommandLine.IndexOf($workerProject, [StringComparison]::OrdinalIgnoreCase) -ge 0)) -or
        ($_.ExecutablePath -and ($_.ExecutablePath.StartsWith($apiOutput, [StringComparison]::OrdinalIgnoreCase) -or $_.ExecutablePath.StartsWith($workerOutput, [StringComparison]::OrdinalIgnoreCase)))
    }

    if ($existingHosts) {
        $processIds = @($existingHosts | Select-Object -ExpandProperty ProcessId -Unique)
        Write-Host "Stopping previous local API/Worker process(es): $($processIds -join ', ')"
        foreach ($processId in $processIds) { & taskkill.exe /PID $processId /T /F *> $null }
        Start-Sleep -Milliseconds 700
    }

    $listeners = @(Get-NetTCPConnection -LocalPort 5080 -State Listen -ErrorAction SilentlyContinue)
    if ($listeners.Count -gt 0) {
        $processIds = ($listeners.OwningProcess | Sort-Object -Unique) -join ', '
        throw "Port 5080 is still occupied by process $processIds, which is not a managed local TODIR API. Stop that process before starting the backend."
    }
}

Push-Location $repositoryRoot
try {
    $postgresAdminPassword = Get-RequiredSecret -Name 'TODIR_DEV_POSTGRES_ADMIN_PASSWORD'
    $rabbitPassword = Get-RequiredSecret -Name 'TODIR_DEV_RABBITMQ_PASSWORD'
    $objectStoragePassword = Get-RequiredSecret -Name 'TODIR_DEV_OBJECT_STORAGE_PASSWORD'

    if (-not (Test-Path -LiteralPath $settingsPath)) {
        throw 'Local settings are missing. Copy backend/appsettings.json to backend/appsettings.Development.json, then configure the local database, services, and Seed:DefaultPassword.'
    }

    $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    $configuredRuntimeConnection = $settings.TODIR_DATABASE_CONNECTION
    if ([string]::IsNullOrWhiteSpace($configuredRuntimeConnection)) { $configuredRuntimeConnection = $settings.ConnectionStrings.Todir }
    if ([string]::IsNullOrWhiteSpace($configuredRuntimeConnection)) {
        throw 'Set TODIR_DATABASE_CONNECTION or ConnectionStrings:Todir in backend/appsettings.Development.json.'
    }

    $databaseHost = Get-ConnectionValue -ConnectionString $configuredRuntimeConnection -Name 'Host'
    if ([string]::IsNullOrWhiteSpace($databaseHost)) { $databaseHost = Get-ConnectionValue -ConnectionString $configuredRuntimeConnection -Name 'Server' }
    if ([string]::IsNullOrWhiteSpace($databaseHost)) { $databaseHost = '127.0.0.1' }
    $databasePort = Get-ConnectionValue -ConnectionString $configuredRuntimeConnection -Name 'Port'
    if ([string]::IsNullOrWhiteSpace($databasePort)) { $databasePort = '15432' }
    $runtimePassword = Get-ConnectionValue -ConnectionString $configuredRuntimeConnection -Name 'Password'
    $seedPassword = $settings.Seed.DefaultPassword
    if ([string]::IsNullOrWhiteSpace($runtimePassword)) { throw 'The runtime database password is missing from the local development connection string.' }
    if ([string]::IsNullOrWhiteSpace($seedPassword)) { throw 'Set Seed:DefaultPassword in backend/appsettings.Development.json before starting the backend.' }

    $databaseSetting = '(?i)(^|;)\s*(Database|Initial Catalog)\s*=\s*("[^"]*"|''[^'']*''|[^;]*)'
    if (-not [regex]::IsMatch($configuredRuntimeConnection, $databaseSetting)) {
        throw 'The local development database connection string must include Database=...'
    }
    $runtimeConnection = [regex]::Replace($configuredRuntimeConnection, $databaseSetting, [System.Text.RegularExpressions.MatchEvaluator]{
        param($match)
        return "$($match.Groups[1].Value)$($match.Groups[2].Value)=$databaseName"
    })

    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    $env:TODIR_REPOSITORY_ROOT = $repositoryRoot
    $env:TODIR_DATABASE_CONNECTION = $runtimeConnection
    $env:TODIR_MIGRATOR_CONNECTION = "Host=$databaseHost;Port=$databasePort;Database=$databaseName;Username=postgres;Password=$postgresAdminPassword"
    $env:TODIR_RUNTIME_PASSWORD = $runtimePassword
    $env:TODIR_RABBITMQ_HOST = if ($settings.TODIR_RABBITMQ_HOST) { $settings.TODIR_RABBITMQ_HOST } else { '127.0.0.1' }
    $env:TODIR_RABBITMQ_PORT = if ($settings.TODIR_RABBITMQ_PORT) { $settings.TODIR_RABBITMQ_PORT } else { '5672' }
    $env:TODIR_RABBITMQ_USER = if ($settings.TODIR_RABBITMQ_USER) { $settings.TODIR_RABBITMQ_USER } else { 'todir' }
    $env:TODIR_RABBITMQ_PASSWORD = $rabbitPassword
    $env:TODIR_RABBITMQ_VHOST = if ($settings.TODIR_RABBITMQ_VHOST) { $settings.TODIR_RABBITMQ_VHOST } else { '/erp-dev' }
    $env:SEED_DEFAULT_PASSWORD = $seedPassword
    $env:TODIR_FILES_BUCKET = if ($settings.TODIR_FILES_BUCKET) { $settings.TODIR_FILES_BUCKET } else { 'todir-files-private' }
    $env:TODIR_FILES_PUBLIC_ENDPOINT = if ($settings.TODIR_FILES_PUBLIC_ENDPOINT) { $settings.TODIR_FILES_PUBLIC_ENDPOINT } else { 'http://127.0.0.1:19000' }
    $env:TODIR_FILES_ACCESS_KEY = if ($settings.TODIR_FILES_ACCESS_KEY) { $settings.TODIR_FILES_ACCESS_KEY } else { 'todir' }
    $env:TODIR_FILES_SECRET_KEY = $objectStoragePassword
    $env:TODIR_FILES_REGION = if ($settings.TODIR_FILES_REGION) { $settings.TODIR_FILES_REGION } else { 'us-east-1' }
    $env:TODIR_FILES_SCANNER_HOST = if ($settings.TODIR_FILES_SCANNER_HOST) { $settings.TODIR_FILES_SCANNER_HOST } else { '127.0.0.1' }
    $env:TODIR_FILES_SCANNER_PORT = if ($settings.TODIR_FILES_SCANNER_PORT) { $settings.TODIR_FILES_SCANNER_PORT } else { '3310' }
    $env:TODIR_POS_OFFLINE_SIGNING_SECRET = if ($env:TODIR_POS_OFFLINE_SIGNING_SECRET) { $env:TODIR_POS_OFFLINE_SIGNING_SECRET } else { 'pos-offline-development-signing-key-2026' }

    Stop-ExistingLocalHosts

    Write-Host 'Starting local PostgreSQL, RabbitMQ, and MinIO services...'
    Invoke-CheckedCommand -Command 'docker' -Arguments @('compose', 'up', '-d') -Description 'Local infrastructure startup'

    Write-Host 'Building the backend once to avoid concurrent writes to shared module assemblies...'
    Invoke-CheckedCommand -Command 'dotnet' -Arguments @('build', 'backend/Backend.sln') -Description 'Backend build'

    Write-Host "Applying migrations and the development seed to '$databaseName'..."
    Invoke-CheckedCommand -Command 'dotnet' -Arguments @('run', '--no-build', '--project', 'backend/DbMigrator/DbMigrator.csproj', '--', '--seed', 'development') -Description 'Database setup'

    Write-Host 'Starting API and Worker without rebuilding. Press Ctrl+C here to stop both.'
    $hostProcesses.Add((Start-Process -FilePath 'dotnet' -ArgumentList @('run', '--no-build', '--project', 'backend/Api/Api.csproj') -WorkingDirectory $repositoryRoot -NoNewWindow -PassThru))
    $hostProcesses.Add((Start-Process -FilePath 'dotnet' -ArgumentList @('run', '--no-build', '--project', 'backend/Worker/Worker.csproj') -WorkingDirectory $repositoryRoot -NoNewWindow -PassThru))
    Write-Host 'API health: http://127.0.0.1:5080/health'

    while ($true) {
        foreach ($hostProcess in $hostProcesses) {
            $hostProcess.Refresh()
            if ($hostProcess.HasExited) { throw 'An API or Worker process exited. Check its output above; stopping the remaining backend process.' }
        }
        Start-Sleep -Seconds 1
    }
}
finally {
    foreach ($hostProcess in $hostProcesses) { Stop-ProcessTree -Process $hostProcess }
    Pop-Location
}
