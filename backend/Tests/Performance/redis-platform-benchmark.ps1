[CmdletBinding()]
param(
    [int]$RequestsPerLevel = 100,
    [string]$Concurrency = '1,10,50,100',
    [int]$ApiPort = 5097,
    [switch]$KeepDatabase
)

# Focused local evidence harness for the shared Redis cache. It creates and
# drops only a todir_redis_perf_* database; it never touches the development DB.
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Run this benchmark with PowerShell 7 (pwsh).' }
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$database = "todir_redis_perf_{0}_{1}" -f (Get-Date -Format 'yyyyMMddHHmmss'), $PID
$artifactDirectory = Join-Path $repo 'artifacts\performance'
$resultPath = Join-Path $artifactDirectory "redis-platform-$database.json"
$apiProcesses = @()
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null

function Invoke-Postgres { param([string]$TargetDatabase, [string]$Sql)
    $output = & docker exec todir-postgres-1 psql -U postgres -d $TargetDatabase -v ON_ERROR_STOP=1 -t -A -c $Sql
    if ($LASTEXITCODE -ne 0) { throw "PostgreSQL command failed for $TargetDatabase" }
    return ($output -join "`n").Trim()
}

function Stop-ProcessSafe { param($Process)
    if ($null -ne $Process -and -not $Process.HasExited) {
        try { $Process.CloseMainWindow() | Out-Null } catch { }
        if (-not $Process.WaitForExit(10000)) { Stop-Process -Id $Process.Id -Force }
    }
}

function Wait-Api { param([string]$BaseUrl, [string]$ErrorLog)
    for ($attempt = 1; $attempt -le 90; $attempt++) {
        try { if ((Invoke-WebRequest -Uri "$BaseUrl/health" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200) { return } } catch { }
        Start-Sleep -Milliseconds 400
    }
    throw "API did not become healthy at $BaseUrl. See $ErrorLog"
}

function Start-Api { param([int]$Port, [bool]$CacheEnabled)
    $stdout = Join-Path $artifactDirectory "redis-api-$database-$Port.out.log"
    $stderr = Join-Path $artifactDirectory "redis-api-$database-$Port.err.log"
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    $env:TODIR_DATABASE_CONNECTION = "Host=127.0.0.1;Port=15432;Database=$database;Username=erp_app;Password=todir-app-dev;Include Error Detail=true;Maximum Pool Size=64;Application Name=redis-platform-perf-$Port"
    $env:TODIR_PERFORMANCE_SQL_COUNT = 'true'
    $env:TODIR_RATE_LIMIT_API_PERMITS = '100000'
    $env:TODIR_RATE_LIMIT_AUTH_PERMITS = '100000'
    $env:TODIR_FILE_LOGGING_ENABLED = 'false'
    $env:TODIR_RABBITMQ_HOST = ''
    $env:TODIR_REDIS_ENABLED = $CacheEnabled.ToString().ToLowerInvariant()
    $env:TODIR_CACHE_HR_DIRECTORY_ENABLED = $CacheEnabled.ToString().ToLowerInvariant()
    $env:TODIR_CACHE_STABLE_REFERENCE_DATA_ENABLED = 'false'
    $env:TODIR_REDIS_CONFIGURATION = '127.0.0.1:6381,abortConnect=false'
    $env:TODIR_CACHE_SCHEMA_VERSION = '97'
    $dll = Join-Path $repo 'backend\Api\bin\Release\net10.0\Api.dll'
    if (-not (Test-Path $dll)) { throw "Build the Release API before the benchmark: $dll" }
    $process = Start-Process dotnet -ArgumentList @($dll, '--urls', "http://127.0.0.1:$Port") -WorkingDirectory $repo -RedirectStandardOutput $stdout -RedirectStandardError $stderr -WindowStyle Hidden -PassThru
    $script:apiProcesses += $process
    Wait-Api "http://127.0.0.1:$Port" $stderr
    return $process
}

function New-Session { param([string]$BaseUrl)
    $csrf = Invoke-WebRequest -Uri "$BaseUrl/api/v1/auth/csrf" -UseBasicParsing -SessionVariable webSession
    $csrfToken = ($csrf.Content | ConvertFrom-Json).token
    $loginBody = @{ email = 'admin@atlas.com'; password = 'TodirDev2026!' } | ConvertTo-Json -Compress
    $login = Invoke-WebRequest -Uri "$BaseUrl/api/v1/auth/login" -Method Post -Headers @{ 'X-CSRF-TOKEN' = $csrfToken } -Body $loginBody -ContentType 'application/json' -UseBasicParsing -WebSession $webSession
    if ($login.StatusCode -ne 200) { throw "Login failed: $($login.Content)" }
    $context = Invoke-WebRequest -Uri "$BaseUrl/api/v1/session/context" -UseBasicParsing -WebSession $webSession
    $cookies = $webSession.Cookies.GetCookies([Uri]$BaseUrl) | ForEach-Object { "$($_.Name)=$($_.Value)" }
    if ([string]::IsNullOrWhiteSpace($cookies)) { throw 'No authentication cookie was issued.' }
    return [pscustomobject]@{ WebSession = $webSession; Cookie = ($cookies -join '; '); Context = ($context.Content | ConvertFrom-Json) }
}

function Get-Percentile { param([double[]]$Values, [double]$Percent)
    $sorted = @($Values | Sort-Object)
    if ($sorted.Count -eq 0) { return 0 }
    $index = [Math]::Max(0, [Math]::Min($sorted.Count - 1, [int][Math]::Ceiling($sorted.Count * $Percent) - 1))
    return [Math]::Round($sorted[$index], 2)
}

function Invoke-Level { param([string]$Url, [string]$Cookie, [int]$Level)
    $started = [DateTimeOffset]::UtcNow
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $results = 1..$RequestsPerLevel | ForEach-Object -Parallel {
        $requestWatch = [Diagnostics.Stopwatch]::StartNew()
        try {
            $response = Invoke-WebRequest -Uri $using:Url -Headers @{ Cookie = $using:Cookie } -UseBasicParsing -TimeoutSec 60 -SkipHttpErrorCheck
            $requestWatch.Stop()
            $header = @($response.Headers['X-Todir-Db-Commands']) | Select-Object -First 1
            [pscustomobject]@{ StatusCode = [int]$response.StatusCode; Milliseconds = $requestWatch.Elapsed.TotalMilliseconds; DbCommands = if ($null -ne $header) { [int]$header } else { $null } }
        }
        catch {
            $requestWatch.Stop()
            [pscustomobject]@{ StatusCode = 0; Milliseconds = $requestWatch.Elapsed.TotalMilliseconds; DbCommands = $null }
        }
    } -ThrottleLimit $Level
    $watch.Stop()
    $latencies = @($results | ForEach-Object { [double]$_.Milliseconds })
    $successes = @($results | Where-Object { $_.StatusCode -ge 200 -and $_.StatusCode -lt 300 })
    $dbCommands = @($results | Where-Object { $null -ne $_.DbCommands } | ForEach-Object { [int]$_.DbCommands })
    [pscustomobject]@{
        Concurrency = $Level
        Requests = $results.Count
        Successes = $successes.Count
        Errors = $results.Count - $successes.Count
        Rps = [Math]::Round($results.Count / [Math]::Max(0.001, $watch.Elapsed.TotalSeconds), 2)
        P50Ms = Get-Percentile $latencies .5
        P95Ms = Get-Percentile $latencies .95
        P99Ms = Get-Percentile $latencies .99
        DbCommandsPerRequest = if ($dbCommands.Count -gt 0) { [Math]::Round((($dbCommands | Measure-Object -Average).Average), 2) } else { $null }
        StartedUtc = $started
    }
}

try {
    if ($database -notmatch '^todir_redis_perf_[a-z0-9_]+$') { throw "Refusing to operate on $database" }
    Invoke-Postgres postgres "create database `"$database`" template template0" | Out-Null
    $env:DOTNET_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:TODIR_REPOSITORY_ROOT = $repo
    $env:TODIR_MIGRATOR_CONNECTION = "Host=127.0.0.1;Port=15432;Database=$database;Username=postgres;Password=postgres;Include Error Detail=true"
    $env:TODIR_RUNTIME_PASSWORD = 'todir-app-dev'
    $env:SEED_DEFAULT_PASSWORD = 'TodirDev2026!'
    & dotnet run --configuration Release --no-build --project (Join-Path $repo 'backend\DbMigrator\DbMigrator.csproj') -- --seed development
    if ($LASTEXITCODE -ne 0) { throw 'Disposable migration and development seed failed.' }

    $tenant = Invoke-Postgres $database "select id::text from platform.tenant where slug='atlas-trading'"
    $entity = Invoke-Postgres $database "select id::text from platform.legal_entity where tenant_id='$tenant' and status='Active' order by id limit 1"
    $levels = @($Concurrency -split ',' | ForEach-Object { [int]$_.Trim() } | Where-Object { $_ -gt 0 })
    if ($levels.Count -eq 0) { throw 'No valid concurrency level.' }
    $endpoint = "/api/v1/hr/employees?legalEntityId=$entity&page=1&pageSize=50"
    $modeResults = @()
    $redisSnapshot = $null

    foreach ($mode in @('postgres-fallback', 'redis-enabled')) {
        $cacheEnabled = $mode -eq 'redis-enabled'
        $process = Start-Api $ApiPort $cacheEnabled
        try {
            $session = New-Session "http://127.0.0.1:$ApiPort"
            1..3 | ForEach-Object { Invoke-WebRequest -Uri "http://127.0.0.1:$ApiPort$endpoint" -Headers @{ Cookie = $session.Cookie } -UseBasicParsing -TimeoutSec 60 -SkipHttpErrorCheck | Out-Null }
            foreach ($level in $levels) {
                $modeResults += [pscustomobject]@{ Mode = $mode; Result = (Invoke-Level "http://127.0.0.1:$ApiPort$endpoint" $session.Cookie $level) }
            }
            if ($cacheEnabled) {
                $info = docker exec todir-redis-1 redis-cli INFO memory
                $usedMemory = ($info | Where-Object { $_ -match '^used_memory:' } | Select-Object -First 1).Split(':')[1]
                $keys = @(docker exec todir-redis-1 redis-cli --scan --pattern 'platform:cache:v97:*:hr.employee.directory:*' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
                $redisSnapshot = [pscustomobject]@{ UsedMemoryBytes = [int64]$usedMemory; MatchingKeys = $keys.Count }
            }
        }
        finally {
            Stop-ProcessSafe $process
        }
        $ApiPort++
    }

    $report = [pscustomobject]@{
        Certification = 'Local Redis platform evidence; not a production capacity claim'
        Commit = (& git rev-parse HEAD).Trim()
        Database = $database
        GeneratedAtUtc = [DateTimeOffset]::UtcNow
        Environment = [pscustomobject]@{ Redis = 'todir-redis-1 / 127.0.0.1:6381'; PostgreSQL = 'todir-postgres-1 / 127.0.0.1:15432'; ApiInstances = 1; PoolMaximum = 64; Seed = 'development / Atlas + Delta' }
        Endpoint = $endpoint
        Results = $modeResults
        Redis = $redisSnapshot
        Limits = @('One local API process per mode; two-instance consistency is covered by RedisPlatformAcceptanceIntegrationTests', '100 concurrent requests and 100 requests per level are the measured local ceiling for this evidence run', 'No production, autoscaling, multi-region, or universal capacity claim')
    }
    $report | ConvertTo-Json -Depth 20 | Set-Content $resultPath -Encoding utf8
    $report | ConvertTo-Json -Depth 12
}
finally {
    foreach ($process in @($apiProcesses)) { Stop-ProcessSafe $process }
    if (-not $KeepDatabase) {
        for ($attempt = 1; $attempt -le 20; $attempt++) {
            try { Invoke-Postgres postgres "drop database `"$database`"" | Out-Null; break }
            catch { if ($attempt -eq 20) { throw }; Start-Sleep -Milliseconds 500 }
        }
    }
}
