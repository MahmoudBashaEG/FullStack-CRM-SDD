#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$devRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$devLocation = Get-Location
$devEnvironment = @{}
$devKeys = @('DOTNET_CLI_HOME','NUGET_PACKAGES','npm_config_cache','DOTNET_CLI_TELEMETRY_OPTOUT',
    'DOTNET_SKIP_FIRST_TIME_EXPERIENCE','DOTNET_ENVIRONMENT','ASPNETCORE_ENVIRONMENT',
    'ASPNETCORE_URLS','DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER','NODE_EXTRA_CA_CERTS','NG_CLI_ANALYTICS')
foreach ($devKey in $devKeys) { $devEnvironment[$devKey] = [Environment]::GetEnvironmentVariable($devKey) }
$devChildren = [Collections.Generic.List[object]]::new()
$devFailure = $null
try {
    if (!$IsWindows -or ![Environment]::Is64BitProcess) { throw 'Use 64-bit PowerShell 7 on Windows.' }
    Set-Location -LiteralPath $devRoot
    . (Join-Path $PSScriptRoot 'environment.ps1')
    if (Get-ChildItem Env: | Where-Object Name -like 'Authentication__Test*') { throw 'Remove Authentication__Test* settings for normal Development.' }
    $devDotnet = (Get-Command dotnet.exe -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
    $devNode = (Get-Command node.exe -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
    $devNpm = (Get-Command npm.cmd -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
    if (!(Test-Path -LiteralPath (Join-Path $devRoot 'src/frontend/node_modules/@angular/cli/bin/ng.js'))) { throw 'Restore frontend dependencies with npm ci --prefix src/frontend first.' }
    & $devDotnet dev-certs https --check
    if ($LASTEXITCODE -ne 0) { throw 'Create/trust the HTTPS development certificate before launch; see the development guide.' }
    if ($env:NODE_EXTRA_CA_CERTS -and !(Test-Path -LiteralPath $env:NODE_EXTRA_CA_CERTS)) { throw 'NODE_EXTRA_CA_CERTS must point to the public API certificate PEM.' }
    $devSslCert = if ($env:CRM_DEV_SSL_CERT) { [IO.Path]::GetFullPath($env:CRM_DEV_SSL_CERT) } else { Join-Path $devRoot '.local/dev-certs/localhost.pem' }
    $devSslKey = if ($env:CRM_DEV_SSL_KEY) { [IO.Path]::GetFullPath($env:CRM_DEV_SSL_KEY) } else { Join-Path $devRoot '.local/dev-certs/localhost.key' }
    if (!(Test-Path -LiteralPath $devSslCert) -or !(Test-Path -LiteralPath $devSslKey)) { throw 'Export the existing trusted development certificate/key to .local/dev-certs first; see the development guide (or set CRM_DEV_SSL_CERT/CRM_DEV_SSL_KEY).' }
    if (!$env:NODE_EXTRA_CA_CERTS) { $env:NODE_EXTRA_CA_CERTS = $devSslCert }
    foreach ($devPort in @(4200,7043)) {
        $devProbe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::IPv6Loopback, $devPort)
        try { $devProbe.Server.DualMode = $true; $devProbe.Start() } catch { throw "Port $devPort is occupied. Stop its owner yourself; no process was taken over." } finally { $devProbe.Stop() }
        $devProbe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $devPort)
        try { $devProbe.Start() } catch { throw "Port $devPort is occupied. Stop its owner yourself; no process was taken over." } finally { $devProbe.Stop() }
    }
    if (!('Crm.Dev.Child' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'DevProcess.cs') }
    [Crm.Dev.Child]::RegisterCancel()
    $env:DOTNET_ENVIRONMENT = $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = 'https://localhost:7043'
    $env:DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER = '1'
    $env:NG_CLI_ANALYTICS = 'false' # First launch must not wait for a telemetry consent prompt.
    Write-Host 'Starting API https://localhost:7043 and frontend https://localhost:4200. Ctrl+C stops both.'
    $devChildren.Add([Crm.Dev.Child]::Start('API/watch', $devDotnet, [string[]]@('watch','--non-interactive','--project', (Join-Path $devRoot 'src/backend/Crm.Api'),'run','--no-launch-profile'), $devRoot))
    # Run the installed npm CLI through Node directly: same npm start, no cmd quoting/prompt wrapper.
    $devNpmCli = Join-Path (Split-Path $devNpm) 'node_modules/npm/bin/npm-cli.js'
    if (!(Test-Path -LiteralPath $devNpmCli)) { throw 'Installed npm CLI could not be located beside npm.cmd.' }
    $devChildren.Add([Crm.Dev.Child]::Start('Angular/npm', $devNode, [string[]]@($devNpmCli,'run','start','--prefix',(Join-Path $devRoot 'src/frontend'),'--','--configuration','development','--port','4200','--ssl','--host','localhost','--ssl-cert',$devSslCert,'--ssl-key',$devSslKey), $devRoot))
    $devClock = [Diagnostics.Stopwatch]::StartNew()
    $devReady = $false
    $devHttp = [Net.Http.HttpClient]::new()
    $devHttp.Timeout = [TimeSpan]::FromSeconds(1)
    try {
        while (![Crm.Dev.Child]::StopRequested) {
            foreach ($devChild in $devChildren) { if ($devChild.HasExited) { throw "$($devChild.Name) exited; stopping owned services. Review the visible logs." } }
            if (!$devReady) {
                try {
                    $devApi = $devHttp.GetAsync('https://localhost:7043/api/ticket-reference-data/categories').GetAwaiter().GetResult()
                    $devUi = $devHttp.GetAsync('https://localhost:4200/').GetAwaiter().GetResult()
                    try {
                        $devReady = [int]$devApi.StatusCode -eq 401 -and [int]$devUi.StatusCode -eq 200 -and $devUi.Content.ReadAsStringAsync().GetAwaiter().GetResult() -match '<html'
                    } finally { $devApi.Dispose(); $devUi.Dispose() }
                } catch { $devReady = $false }
                if ($devReady) { Write-Host 'Both servers are available. Open https://localhost:4200. Sign in with a local CRM account; bootstrap the initial Admin separately.' }
                elseif ($devClock.Elapsed.TotalSeconds -ge 120) { throw 'Startup availability timed out (120 seconds). Check build, SQL, local authentication and certificate trust setup in the visible logs.' }
            }
            Start-Sleep -Milliseconds 200
        }
    } finally { $devHttp.Dispose() }
} catch { $devFailure = $_.Exception.Message }
finally {
    if ($devChildren.Count -gt 0) {
        try { [Crm.Dev.Child]::StopAll([Crm.Dev.Child[]]$devChildren.ToArray(),5000) | ForEach-Object { Write-Host $_ } }
        catch { $devFailure = 'Owned process cleanup failed; inspect the launcher processes before restarting.' }
        finally { foreach ($devChild in $devChildren) { $devChild.Dispose() } }
    }
    if ('Crm.Dev.Child' -as [type]) { [Crm.Dev.Child]::UnregisterCancel() }
    foreach ($devKey in $devKeys) {
        if ($null -eq $devEnvironment[$devKey]) { Remove-Item -LiteralPath "Env:$devKey" -ErrorAction SilentlyContinue }
        else { [Environment]::SetEnvironmentVariable($devKey,[string]$devEnvironment[$devKey]) }
    }
    Set-Location -LiteralPath $devLocation
}
if ($devFailure) {
    Write-Host "Development startup stopped: $devFailure" -ForegroundColor Red
    if ($MyInvocation.InvocationName -eq '.') { throw 'Development startup failed; see setup diagnostics.' }
    exit 1
}
