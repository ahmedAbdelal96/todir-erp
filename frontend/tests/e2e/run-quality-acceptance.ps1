[CmdletBinding()]
param(
  [string]$Grep = ""
)

$ErrorActionPreference = "Stop"

if (-not $env:TODIR_ACCEPTANCE_DB) { $env:TODIR_ACCEPTANCE_DB = "todir_quality_acceptance" }
if (-not $env:TODIR_DB_PASSWORD -and $env:ERP_DEV_PASSWORD) { $env:TODIR_DB_PASSWORD = $env:ERP_DEV_PASSWORD }
if (-not $env:TODIR_ACCEPTANCE_PASSWORD -and $env:SEED_DEFAULT_PASSWORD) { $env:TODIR_ACCEPTANCE_PASSWORD = $env:SEED_DEFAULT_PASSWORD }
if (-not $env:TODIR_ACCEPTANCE_DIAGNOSTIC_TOKEN) {
  throw "FATAL SAFETY VIOLATION: TODIR_ACCEPTANCE_DIAGNOSTIC_TOKEN is not set in environment. Acceptance diagnostics are restricted to isolated authorized local environments."
}

Set-Location (Join-Path $PSScriptRoot "../..")

if ($Grep) {
  npx playwright test --config=playwright.quality.config.ts -g $Grep
} else {
  npx playwright test --config=playwright.quality.config.ts
}

if ($LASTEXITCODE -ne 0) {
  exit $LASTEXITCODE
}


