$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'platform-performance-helpers.ps1')

function Assert-Equal {
    param($Expected, $Actual, [string]$Message)
    if ($Expected -ne $Actual) { throw "$Message. Expected '$Expected', got '$Actual'." }
}

$diagnostic = Get-ModeProfile -Mode diagnostic
Assert-Equal 5 $diagnostic.RequestsPerEndpoint 'Diagnostic request count'
Assert-Equal $false $diagnostic.SustainedWrite 'Diagnostic sustained-write flag'
Assert-Equal 2 $diagnostic.ConcurrencyLevels.Count 'Diagnostic concurrency levels'

$load = Get-ModeProfile -Mode load -Concurrency '1,4,8'
Assert-Equal 40 $load.RequestsPerEndpoint 'Load request count'
Assert-Equal $true $load.SustainedWrite 'Load sustained-write flag'
Assert-Equal 3 $load.ConcurrencyLevels.Count 'Load concurrency levels'

$stress = Get-ModeProfile -Mode stress -Concurrency '1,4,8,16'
Assert-Equal 100 $stress.RequestsPerEndpoint 'Stress request count'
Assert-Equal 4 $stress.ConcurrencyLevels.Count 'Stress concurrency levels'
$invalid = $false
try { Get-ModeProfile -Mode load -Concurrency '1,4,8,16' | Out-Null } catch { $invalid = $true }
Assert-Equal $true $invalid 'Load level bound'
$invalid = $false
try { Get-ModeProfile -Mode stress -Concurrency '0,4' | Out-Null } catch { $invalid = $true }
Assert-Equal $true $invalid 'Invalid concurrency rejection'

$small = Get-LatencySummary -Values @(10, 20, 30, 40, 50)
Assert-Equal 'MEASURED' $small.P50Status 'P50 status'
Assert-Equal 'INSUFFICIENT_SAMPLES' $small.P95Status 'P95 minimum sample status'
Assert-Equal 'INSUFFICIENT_SAMPLES' $small.P99Status 'P99 minimum sample status'
$empty = Get-LatencySummary -Values @()
Assert-Equal 'NO_SAMPLES' $empty.P50Status 'Empty P50 status'
Assert-Equal $null $empty.P50Ms 'Empty P50 value'

$errors = Get-ErrorSummary -Results @(
    [pscustomobject]@{ Success = $true; ErrorCategory = $null },
    [pscustomobject]@{ Success = $false; ErrorCategory = 'Http5xx' },
    [pscustomobject]@{ Success = $false; ErrorCategory = 'Timeout' }
)
Assert-Equal 3 $errors.Requests 'Error summary requests'
Assert-Equal 2 $errors.Errors 'Error summary errors'
Assert-Equal 0.6667 $errors.ErrorRate 'Error rate'
Assert-Equal 1 $errors.ErrorDistribution.Http5xx 'HTTP error distribution'

Assert-DisposableDatabaseName 'todir_perf_test_1'
$rejected = $false
try { Assert-DisposableDatabaseName 'todir' } catch { $rejected = $true }
Assert-Equal $true $rejected 'Shared database rejection'

$container = [pscustomobject]@{
    Name = '/todir-p0b-postgres-test'
    State = [pscustomobject]@{ Status = 'running' }
    Config = [pscustomobject]@{ Image = 'postgres:18'; Labels = @{ 'todir.perf.disposable' = 'true' } }
}
Assert-DisposableContainerMetadata $container
$rejected = $false
try { $container.Config.Labels['todir.perf.disposable'] = 'false'; Assert-DisposableContainerMetadata $container | Out-Null } catch { $rejected = $true }
Assert-Equal $true $rejected 'Unlabelled container rejection'

Write-Output 'platform-performance-harness.tests.ps1: PASS'
