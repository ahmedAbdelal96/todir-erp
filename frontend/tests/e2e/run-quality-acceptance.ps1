[CmdletBinding()]
param(
  [string]$Grep = ""
)

$ErrorActionPreference = "Stop"

if (-not $env:TODIR_ACCEPTANCE_DB) { $env:TODIR_ACCEPTANCE_DB = "todir_quality_acceptance" }
if (-not $env:TODIR_DB_PASSWORD -and $env:ERP_DEV_PASSWORD) { $env:TODIR_DB_PASSWORD = $env:ERP_DEV_PASSWORD }
if (-not $env:TODIR_ACCEPTANCE_DIAGNOSTIC_TOKEN) { $env:TODIR_ACCEPTANCE_DIAGNOSTIC_TOKEN = "acceptance-diag-token-dev-2026" }

Set-Location (Join-Path $PSScriptRoot "../..")

if ($Grep) {
  npx playwright test --config=playwright.quality.config.ts -g $Grep
} else {
  npx playwright test --config=playwright.quality.config.ts
}

