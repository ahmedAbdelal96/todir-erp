[CmdletBinding()]
param(
    [int]$RequestsPerEndpoint = 40,
    [string]$Concurrency = '1,4,8',
    [int]$ApiPort = 5091,
    [switch]$KeepDatabase
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$database = "todir_perf_{0}_{1}" -f (Get-Date -Format 'yyyyMMddHHmmss'), $PID
$apiBaseUrl = "http://127.0.0.1:$ApiPort"
$apiLogDirectory = Join-Path $repo 'artifacts\performance'
$apiStdout = Join-Path $apiLogDirectory "api-$database.out.log"
$apiStderr = Join-Path $apiLogDirectory "api-$database.err.log"
$resultPath = Join-Path $apiLogDirectory "platform-performance-$database.json"
$apiProcess = $null
$client = $null

New-Item -ItemType Directory -Path $apiLogDirectory -Force | Out-Null

function Assert-DisposableDatabaseName {
    if ($database -notmatch '^todir_perf_[a-z0-9_]+$') {
        throw "Refusing to operate on a non-disposable database name: $database"
    }
}

function Invoke-Postgres {
    param([string]$TargetDatabase, [string]$Sql)
    $output = & docker exec todir-postgres-1 psql -U postgres -d $TargetDatabase -v ON_ERROR_STOP=1 -t -A -c $Sql
    if ($LASTEXITCODE -ne 0) { throw "PostgreSQL command failed for $TargetDatabase." }
    return ($output -join "`n").Trim()
}

function Invoke-HttpJson {
    param([string]$Method, [string]$Url, [object]$Body = $null, [hashtable]$Headers = @{})
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::$Method, $Url)
    foreach ($header in $Headers.GetEnumerator()) { [void]$request.Headers.TryAddWithoutValidation($header.Key, [string]$header.Value) }
    if ($null -ne $Body) {
        $json = $Body | ConvertTo-Json -Compress -Depth 10
        $request.Content = [System.Net.Http.StringContent]::new($json, [System.Text.Encoding]::UTF8, 'application/json')
    }
    $response = $client.SendAsync($request).GetAwaiter().GetResult()
    $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    return [pscustomobject]@{ StatusCode = [int]$response.StatusCode; Content = $content }
}

function Wait-Api {
    for ($attempt = 1; $attempt -le 60; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri "$apiBaseUrl/health" -UseBasicParsing -TimeoutSec 2
            if ($response.StatusCode -eq 200) { return }
        } catch { }
        Start-Sleep -Milliseconds 500
    }
    throw "API did not become healthy at $apiBaseUrl. See $apiStderr."
}

function Start-Api {
    param([bool]$CacheEnabled)
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    $env:TODIR_DATABASE_CONNECTION = "Host=127.0.0.1;Port=15432;Database=$database;Username=erp_app;Password=todir-app-dev;Include Error Detail=true"
    $env:TODIR_METRICS_ENABLED = 'true'
    $env:TODIR_AUTHORIZATION_REQUEST_CACHE_ENABLED = $CacheEnabled.ToString().ToLowerInvariant()
    $env:TODIR_RATE_LIMIT_API_PERMITS = '100000'
    $env:TODIR_RATE_LIMIT_AUTH_PERMITS = '100000'
    $env:TODIR_FILE_LOGGING_ENABLED = 'false'
    $env:TODIR_RABBITMQ_HOST = ''
    $apiDll = Join-Path $repo 'backend\Api\bin\Release\net10.0\Api.dll'
    if (-not (Test-Path $apiDll)) { throw "Build the Release API before running this benchmark: $apiDll" }
    $script:apiProcess = Start-Process -FilePath 'dotnet' -ArgumentList @($apiDll, '--urls', $apiBaseUrl) -WorkingDirectory $repo -RedirectStandardOutput $apiStdout -RedirectStandardError $apiStderr -WindowStyle Hidden -PassThru
    Wait-Api
}

function Stop-Api {
    if ($null -ne $script:apiProcess -and -not $script:apiProcess.HasExited) {
        $script:apiProcess.CloseMainWindow() | Out-Null
        if (-not $script:apiProcess.WaitForExit(10000)) { Stop-Process -Id $script:apiProcess.Id -Force }
    }
    $script:apiProcess = $null
}

function Login {
    $csrfResponse = Invoke-HttpJson -Method Get -Url "$apiBaseUrl/api/v1/auth/csrf"
    if ($csrfResponse.StatusCode -ne 200) { throw "CSRF bootstrap failed with HTTP $($csrfResponse.StatusCode)." }
    $csrf = ($csrfResponse.Content | ConvertFrom-Json).token
    $login = Invoke-HttpJson -Method Post -Url "$apiBaseUrl/api/v1/auth/login" -Body @{ email = 'admin@atlas.com'; password = 'TodirDev2026!' } -Headers @{ 'X-CSRF-TOKEN' = $csrf }
    if ($login.StatusCode -ne 200) { throw "Development login failed with HTTP $($login.StatusCode): $($login.Content)" }
    $snapshot = Invoke-HttpJson -Method Get -Url "$apiBaseUrl/api/v1/session/context"
    if ($snapshot.StatusCode -ne 200) { throw "Session context failed with HTTP $($snapshot.StatusCode): $($snapshot.Content)" }
    return ($snapshot.Content | ConvertFrom-Json)
}

function Get-MetricValue {
    param([string]$Metrics, [string]$MetricName)
    $pattern = "(?m)^$MetricName(?:_total)?(?:\{[^}]*\})?\s+([0-9\.]+)"
    $match = [regex]::Match($Metrics, $pattern)
    if ($match.Success) { return [double]$match.Groups[1].Value }
    return 0
}

function Invoke-BenchmarkRequest {
    param([string]$Url)
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $response = $client.GetAsync($Url).GetAwaiter().GetResult()
        $watch.Stop()
        [void]$response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
        return [pscustomobject]@{ StatusCode = [int]$response.StatusCode; Milliseconds = $watch.Elapsed.TotalMilliseconds }
    } catch {
        $watch.Stop()
        return [pscustomobject]@{ StatusCode = 0; Milliseconds = $watch.Elapsed.TotalMilliseconds }
    }
}

function Invoke-EndpointBenchmark {
    param([string]$Name, [string]$Url, [string]$CookieHeader, [int]$Level)
    1..5 | ForEach-Object { [void](Invoke-BenchmarkRequest -Url $Url) }
    $started = [System.Diagnostics.Stopwatch]::StartNew()
    $results = 1..$RequestsPerEndpoint | ForEach-Object -Parallel {
        $watch = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            $response = Invoke-WebRequest -Uri $using:Url -Headers @{ Cookie = $using:CookieHeader } -UseBasicParsing -TimeoutSec 30
            $watch.Stop()
            [pscustomobject]@{ StatusCode = [int]$response.StatusCode; Milliseconds = $watch.Elapsed.TotalMilliseconds }
        } catch {
            $watch.Stop()
            [pscustomobject]@{ StatusCode = 0; Milliseconds = $watch.Elapsed.TotalMilliseconds }
        }
    } -ThrottleLimit $Level
    $started.Stop()
    $latencies = @($results | ForEach-Object { [double]$_.Milliseconds } | Sort-Object)
    $successes = @($results | Where-Object { $_.StatusCode -ge 200 -and $_.StatusCode -lt 300 })
    $errors = $results.Count - $successes.Count
    $percentile = {
        param([double]$p)
        if ($latencies.Count -eq 0) { return 0 }
        $index = [Math]::Max(0, [Math]::Min($latencies.Count - 1, [int][Math]::Ceiling($latencies.Count * $p) - 1))
        return [Math]::Round($latencies[$index], 2)
    }
    return [pscustomobject]@{
        Endpoint = $Name
        Concurrency = $Level
        Requests = $results.Count
        Successes = $successes.Count
        Errors = $errors
        RequestsPerSecond = [Math]::Round($results.Count / [Math]::Max(0.001, $started.Elapsed.TotalSeconds), 2)
        P50Ms = & $percentile 0.50
        P95Ms = & $percentile 0.95
        P99Ms = & $percentile 0.99
    }
}

Assert-DisposableDatabaseName
try {
    Invoke-Postgres -TargetDatabase 'postgres' -Sql "create database `"$database`" template template0" | Out-Null
    $env:DOTNET_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:TODIR_REPOSITORY_ROOT = $repo
    $env:TODIR_MIGRATOR_CONNECTION = "Host=127.0.0.1;Port=15432;Database=$database;Username=postgres;Password=postgres;Include Error Detail=true"
    $env:TODIR_RUNTIME_PASSWORD = 'todir-app-dev'
    $env:SEED_DEFAULT_PASSWORD = 'TodirDev2026!'
    & dotnet run --configuration Release --no-build --project (Join-Path $repo 'backend\DbMigrator\DbMigrator.csproj') -- --seed development
    if ($LASTEXITCODE -ne 0) { throw 'Disposable migration and development seed failed.' }

    $partyId = Invoke-Postgres -TargetDatabase $database -Sql "select party_id::text from finance_subledger.party_open_item where tenant_id=(select id from platform.tenant where slug='atlas-trading') and legal_entity_id=(select id from platform.legal_entity where tenant_id=(select id from platform.tenant where slug='atlas-trading') and status='Active' order by id limit 1) and is_open=true order by id limit 1"
    if ([string]::IsNullOrWhiteSpace($partyId)) { throw 'Seeded Atlas open-item party was not found; refusing to benchmark a synthetic endpoint.' }

    $phaseResults = @()
    $concurrencyLevels = @($Concurrency -split ',' | ForEach-Object { [int]$_.Trim() } | Where-Object { $_ -gt 0 })
    if ($concurrencyLevels.Count -eq 0) { throw 'At least one positive concurrency level is required.' }
    foreach ($cacheEnabled in @($false, $true)) {
        $clientHandler = [System.Net.Http.HttpClientHandler]::new()
        $clientHandler.UseCookies = $true
        $client = [System.Net.Http.HttpClient]::new($clientHandler)
        $client.Timeout = [TimeSpan]::FromSeconds(30)
        Start-Api -CacheEnabled $cacheEnabled
        try {
            $session = Login
            $entityId = [string]$session.context.legalEntities[0].id
            $endpoint = "$apiBaseUrl/api/v1/subledger/parties/$partyId/exposure?legalEntityId=$entityId&direction=Receivable&asOfDate=2026-11-22"
            $cookieHeader = ($clientHandler.CookieContainer.GetCookies([Uri]$apiBaseUrl) | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join '; '
            if ([string]::IsNullOrWhiteSpace($cookieHeader)) { throw 'Authenticated session cookie was not established.' }
            $metricsBefore = (Invoke-WebRequest -Uri "$apiBaseUrl/internal/metrics" -UseBasicParsing).Content
            foreach ($level in $concurrencyLevels) {
                $phaseResults += Invoke-EndpointBenchmark -Name 'subledger.party.exposure' -Url $endpoint -CookieHeader $cookieHeader -Level $level
            }
            $metricsAfter = (Invoke-WebRequest -Uri "$apiBaseUrl/internal/metrics" -UseBasicParsing).Content
            $phaseResults += [pscustomobject]@{
                Endpoint = 'authorization.subject.metrics'
                CacheEnabled = $cacheEnabled
                DatabaseLoads = (Get-MetricValue -Metrics $metricsAfter -MetricName 'todir_authorization_subject_database_loads') - (Get-MetricValue -Metrics $metricsBefore -MetricName 'todir_authorization_subject_database_loads')
                RequestCacheHits = (Get-MetricValue -Metrics $metricsAfter -MetricName 'todir_authorization_subject_request_cache_hits') - (Get-MetricValue -Metrics $metricsBefore -MetricName 'todir_authorization_subject_request_cache_hits')
            }
        } finally {
            Stop-Api
            $client.Dispose()
            $client = $null
        }
    }

    $report = [pscustomobject]@{
        Commit = (& git rev-parse HEAD).Trim()
        Database = $database
        Endpoint = 'subledger.party.exposure'
        RequestsPerEndpoint = $RequestsPerEndpoint
        Concurrency = $concurrencyLevels
        Results = $phaseResults
        GeneratedAtUtc = [DateTimeOffset]::UtcNow
    }
    $report | ConvertTo-Json -Depth 8 | Set-Content -Path $resultPath -Encoding utf8
    $report | ConvertTo-Json -Depth 8
} finally {
    Stop-Api
    if ($null -ne $client) { $client.Dispose() }
    if (-not $KeepDatabase) {
        for ($attempt = 1; $attempt -le 20; $attempt++) {
            try {
                Invoke-Postgres -TargetDatabase 'postgres' -Sql "drop database `"$database`"" | Out-Null
                break
            } catch {
                if ($attempt -eq 20) { throw "Could not drop disposable database $database without terminating active sessions." }
                Start-Sleep -Milliseconds 500
            }
        }
    }
}
