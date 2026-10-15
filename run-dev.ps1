<#
.SYNOPSIS
  Starts the CRM backend (http://localhost:5080) and frontend (http://localhost:4200) for local development.

.DESCRIPTION
  1. Applies pending EF Core migrations to the database from the user secret (unless -SkipMigrations).
  2. Opens the API and the Angular dev server, each in its own PowerShell window.

.EXAMPLE
  .\run-dev.ps1
  .\run-dev.ps1 -SkipMigrations
#>
param(
    [switch]$SkipMigrations
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$backend = Join-Path $root 'backend'
$frontend = Join-Path $root 'frontend'

if (-not $SkipMigrations) {
    Write-Host 'Applying pending migrations...' -ForegroundColor Cyan
    Push-Location $backend
    try {
        dotnet ef database update `
            --project src/Crm.Infrastructure `
            --startup-project src/Crm.Api `
            -- --environment Development
        if ($LASTEXITCODE -ne 0) {
            throw "Migration failed (exit code $LASTEXITCODE). Fix the error above, or rerun with -SkipMigrations."
        }
    }
    finally {
        Pop-Location
    }
}

if (-not (Test-Path (Join-Path $frontend 'node_modules'))) {
    Write-Host 'Installing frontend dependencies...' -ForegroundColor Cyan
    Push-Location $frontend
    try { npm install } finally { Pop-Location }
}

Write-Host 'Starting API on http://localhost:5080 ...' -ForegroundColor Green
Start-Process pwsh -WorkingDirectory $backend -ArgumentList @(
    '-NoExit', '-Command',
    '$Host.UI.RawUI.WindowTitle = "CRM API"; dotnet run --project src/Crm.Api --launch-profile http'
)

Write-Host 'Starting Angular on http://localhost:4200 ...' -ForegroundColor Green
Start-Process pwsh -WorkingDirectory $frontend -ArgumentList @(
    '-NoExit', '-Command',
    '$Host.UI.RawUI.WindowTitle = "CRM Frontend"; npm start'
)

Write-Host 'Both started. Open http://localhost:4200 (close the two windows to stop).' -ForegroundColor Cyan
