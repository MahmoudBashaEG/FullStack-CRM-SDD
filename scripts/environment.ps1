# Workspace-local caches keep build state out of tracked sources.
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '../.local/dotnet'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '../.local/nuget'
$env:npm_config_cache = Join-Path $PSScriptRoot '../.local/npm-cache'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
