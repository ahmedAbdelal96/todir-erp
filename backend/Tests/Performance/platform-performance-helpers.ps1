Set-StrictMode -Version Latest

function Get-ModeProfile {
    param(
        [ValidateSet('diagnostic', 'load', 'stress')]
        [string]$Mode,
        [int]$RequestsPerEndpoint = 0,
        [string]$Concurrency = ''
    )

    $defaults = switch ($Mode) {
        'diagnostic' { [pscustomobject]@{ Requests = 5; Warmup = 2; Levels = @(1, 4); MaxLevels = 2; SustainedWrite = $false; MultiInstance = $false; AbortErrorRate = 1.0; AbortP95Ms = 30000 } }
        'load'       { [pscustomobject]@{ Requests = 40; Warmup = 5; Levels = @(1, 4, 8); MaxLevels = 3; SustainedWrite = $true; MultiInstance = $true; AbortErrorRate = 0.10; AbortP95Ms = 30000 } }
        'stress'     { [pscustomobject]@{ Requests = 100; Warmup = 10; Levels = @(1, 4, 8, 16); MaxLevels = 4; SustainedWrite = $true; MultiInstance = $true; AbortErrorRate = 0.10; AbortP95Ms = 30000 } }
    }

    $levels = if ([string]::IsNullOrWhiteSpace($Concurrency)) { @($defaults.Levels) } else {
        @($Concurrency -split ',' | ForEach-Object {
            $parsed = 0
            if (-not [int]::TryParse($_.Trim(), [ref]$parsed) -or $parsed -lt 1) { throw "Concurrency must contain positive integers: '$Concurrency'" }
            $parsed
        })
    }
    if ($levels.Count -gt $defaults.MaxLevels) { throw "$Mode mode permits at most $($defaults.MaxLevels) concurrency levels." }
    if ($Mode -eq 'stress' -and ($levels | Measure-Object -Maximum).Maximum -gt 32) { throw 'Stress mode is bounded at concurrency 32.' }
    if ($RequestsPerEndpoint -lt 0) { throw 'RequestsPerEndpoint cannot be negative.' }

    [pscustomobject]@{
        Mode = $Mode
        RequestsPerEndpoint = if ($RequestsPerEndpoint -gt 0) { $RequestsPerEndpoint } else { $defaults.Requests }
        WarmupRequests = $defaults.Warmup
        ConcurrencyLevels = @($levels)
        SustainedWrite = $defaults.SustainedWrite
        MultiInstance = $defaults.MultiInstance
        AbortErrorRate = $defaults.AbortErrorRate
        AbortP95Ms = $defaults.AbortP95Ms
        PercentileMethod = 'nearest-rank (ceil(p*n), one-based)'
    }
}

function Get-PercentileSummary {
    param(
        [AllowEmptyCollection()]
        [double[]]$Values,
        [ValidateRange(0, 1)]
        [double]$Percent,
        [int]$MinimumSamples = 1
    )

    $samples = @($Values | Where-Object { $_ -ge 0 -and -not [double]::IsNaN($_) -and -not [double]::IsInfinity($_) })
    if ($samples.Count -eq 0) { return [pscustomobject]@{ Value = $null; Status = 'NO_SAMPLES'; Samples = 0 } }
    if ($samples.Count -lt $MinimumSamples) { return [pscustomobject]@{ Value = $null; Status = 'INSUFFICIENT_SAMPLES'; Samples = $samples.Count } }
    $sorted = @($samples | Sort-Object)
    $rank = [Math]::Max(1, [int][Math]::Ceiling($sorted.Count * $Percent))
    [pscustomobject]@{ Value = [Math]::Round([double]$sorted[$rank - 1], 2); Status = 'MEASURED'; Samples = $samples.Count }
}

function Get-LatencySummary {
    param([AllowEmptyCollection()][double[]]$Values)
    $p50 = Get-PercentileSummary -Values $Values -Percent .50 -MinimumSamples 2
    $p95 = Get-PercentileSummary -Values $Values -Percent .95 -MinimumSamples 20
    $p99 = Get-PercentileSummary -Values $Values -Percent .99 -MinimumSamples 100
    [pscustomobject]@{
        SampleCount = $p50.Samples
        P50Ms = $p50.Value
        P50Status = $p50.Status
        P95Ms = $p95.Value
        P95Status = $p95.Status
        P99Ms = $p99.Value
        P99Status = $p99.Status
        MaxMs = if ($p50.Samples -gt 0) { [Math]::Round([double](@($Values | Measure-Object -Maximum).Maximum), 2) } else { $null }
    }
}

function Get-ErrorSummary {
    param([AllowEmptyCollection()][object[]]$Results)
    $items = @($Results)
    $errors = @($items | Where-Object { -not $_.Success })
    $distribution = [ordered]@{}
    foreach ($group in @($errors | Group-Object ErrorCategory)) { $distribution[$group.Name] = $group.Count }
    [pscustomobject]@{
        Requests = $items.Count
        Successes = @($items | Where-Object Success).Count
        Errors = $errors.Count
        ErrorRate = if ($items.Count -eq 0) { $null } else { [Math]::Round($errors.Count / $items.Count, 4) }
        ErrorDistribution = [pscustomobject]$distribution
    }
}

function Assert-DisposableDatabaseName {
    param([Parameter(Mandatory)][string]$Database)
    if ($Database -notmatch '^todir_perf_[a-z0-9_]+$') { throw "Refusing to operate on non-disposable database name '$Database'." }
}

function Assert-DisposableContainerMetadata {
    param([Parameter(Mandatory)][object]$Inspect)
    $name = [string]$Inspect.Name
    $running = [string]$Inspect.State.Status -eq 'running'
    $label = [string]$Inspect.Config.Labels.'todir.perf.disposable'
    $image = [string]$Inspect.Config.Image
    if (-not $running) { throw "Disposable PostgreSQL container '$name' is not running." }
    if ($label -ne 'true') { throw "Refusing container '$name': label todir.perf.disposable=true is required." }
    if ($name -notmatch '^/todir-p0b-postgres-[a-z0-9-]+$') { throw "Refusing container '$name': expected a P0-B-owned name." }
    if ($image -notmatch '^postgres(:|@)') { throw "Refusing non-PostgreSQL image '$image'." }
    return $true
}

function New-RunId {
    ('{0}_{1}_{2}' -f (Get-Date -Format 'yyyyMMddHHmmss'), $PID, ([Guid]::NewGuid().ToString('N').Substring(0, 8))).ToLowerInvariant()
}
