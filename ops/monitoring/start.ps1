[CmdletBinding()]
param([switch]$Pull)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$artifactRoot = Join-Path $repositoryRoot "artifacts/monitoring"
$tokenPath = Join-Path $artifactRoot "metrics-token"
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
if (-not (Test-Path -LiteralPath $tokenPath)) {
    [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)) | Set-Content -LiteralPath $tokenPath -NoNewline
}
$env:COMPOSE_PROJECT_NAME = "todir-monitoring"
$compose = Join-Path $repositoryRoot "ops/compose/platform-monitoring.yml"
if ($Pull) { docker compose -f $compose pull }
docker compose -f $compose up -d
Write-Output "Monitoring stack started. Use metrics token: $tokenPath"
Write-Output "Prometheus: http://127.0.0.1:19090 | Alertmanager: http://127.0.0.1:19093 | Traces: http://127.0.0.1:16686"
