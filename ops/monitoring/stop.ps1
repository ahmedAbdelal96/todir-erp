[CmdletBinding()]
param([switch]$RemoveData)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$env:COMPOSE_PROJECT_NAME = "todir-monitoring"
$compose = Join-Path $repositoryRoot "ops/compose/platform-monitoring.yml"
docker compose -f $compose down
if ($RemoveData) { docker compose -f $compose down --volumes }
Write-Output "Monitoring stack stopped. ERP data and volumes were not modified."
