param(
    [Parameter(Mandatory = $true)]
    [int] $Port
)

$ErrorActionPreference = "Stop"
$apiRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$apiRootPrefix = $apiRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

$connections = @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
if ($connections.Count -eq 0) {
    exit 0
}

$ownerIds = @($connections | Select-Object -ExpandProperty OwningProcess -Unique)
foreach ($ownerId in $ownerIds) {
    $process = Get-Process -Id $ownerId -ErrorAction SilentlyContinue
    if ($null -eq $process) {
        continue
    }

    $isApiProcess = $false
    if ($process.Path) {
        $isApiProcess = $process.ProcessName -eq "Api" -and $process.Path.StartsWith($apiRootPrefix, [System.StringComparison]::OrdinalIgnoreCase)
    }

    if (-not $isApiProcess) {
        throw "Development port $Port is already used by '$($process.ProcessName)' (PID $ownerId). The process was not stopped."
    }

    Write-Host "Stopping existing TODIR API process (PID $ownerId) on port $Port..."
    Stop-Process -Id $ownerId -Force
    Wait-Process -Id $ownerId -Timeout 10 -ErrorAction SilentlyContinue
}

