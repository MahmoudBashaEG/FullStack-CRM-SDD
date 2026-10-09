param([switch]$PreflightOnly)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
function Assert([bool]$condition, [string]$message) { if (!$condition) { throw $message } }
Assert (Test-Path "$root/scripts/run-dev.ps1") 'Missing root launcher (expected red before T092).'
Assert (Test-Path "$root/scripts/DevProcess.cs") 'Missing owned-process helper.'
if ($PreflightOnly) { Write-Output 'Launcher artifacts exist.'; return }
. "$root/scripts/environment.ps1"
dotnet build "$PSScriptRoot/fixtures/DevFixture/DevFixture.csproj" --nologo --verbosity quiet
Assert ($LASTEXITCODE -eq 0) 'Fixture build failed.'
if (!('Crm.Dev.Child' -as [type])) { Add-Type -Path "$root/scripts/DevProcess.cs" }
$fixture = "$PSScriptRoot/fixtures/DevFixture/bin/Debug/net10.0/DevFixture.exe"
$unrelated = [Diagnostics.Process]::Start($fixture, 'wait')
try {
    foreach ($mode in @('cooperative', 'uncooperative', 'orphan')) {
        $env:DEV_FIXTURE_MODE = $mode
        $child = [Crm.Dev.Child]::Start($mode, $fixture, [string[]]@('tree'), $root)
        Start-Sleep -Milliseconds 700
        Assert ($child.ActiveCount -ge 1) 'No owned fixture processes.'
        $elapsed = [Diagnostics.Stopwatch]::StartNew()
        $outcomes = [Crm.Dev.Child]::StopAll([Crm.Dev.Child[]]@($child), 5000)
        Assert ($child.ActiveCount -eq 0) 'Owned descendants survived shutdown.'
        Assert ($elapsed.Elapsed.TotalSeconds -lt 9) 'Shutdown exceeded bounded grace/fallback.'
        if ($mode -eq 'cooperative') { Assert ($outcomes[0] -match 'graceful') 'Cooperative fixture was forcibly stopped.' }
        else { Assert ($outcomes[0] -match 'forced') 'Uncooperative/orphan fixture did not exercise fallback.'; Assert ($elapsed.ElapsedMilliseconds -ge 4900) 'Forced cleanup skipped the shared five-second grace budget.' }
        Write-Output "PASS $mode ownership: $($outcomes[0]); $($elapsed.ElapsedMilliseconds) ms; zero owned descendants."
        Assert (!$unrelated.HasExited) 'Unrelated control process was stopped.'
        $child.Dispose()
    }
    # Real console Ctrl+C is delivered by an isolated native host, not simulated by killing pwsh.
    & $fixture console-launch "$root/scripts/run-dev.ps1" "$root" (Get-Command pwsh.exe | Select-Object -First 1).Source
    Assert ($LASTEXITCODE -eq 0) 'Console Ctrl+C / launcher lifecycle tests failed.'
    $locationBefore=Get-Location
    $names=@('DOTNET_CLI_HOME','NUGET_PACKAGES','npm_config_cache','DOTNET_CLI_TELEMETRY_OPTOUT','DOTNET_SKIP_FIRST_TIME_EXPERIENCE','DOTNET_ENVIRONMENT','ASPNETCORE_ENVIRONMENT','ASPNETCORE_URLS','DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER','NODE_EXTRA_CA_CERTS','NG_CLI_ANALYTICS')
    $before=@{};foreach($name in $names){$before[$name]=[Environment]::GetEnvironmentVariable($name)}
    $blocker=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,4200);$blocker.Start()
    try { try { . "$root/scripts/run-dev.ps1"; throw 'Occupied-port launcher unexpectedly succeeded.' } catch { Assert ($_.Exception.Message -eq 'Development startup failed; see setup diagnostics.') 'Unexpected in-process failure.' } }
    finally { $blocker.Stop() }
    Assert ((Get-Location).Path -eq $locationBefore.Path) 'Caller location not restored.'
    foreach($name in $names){Assert ([Environment]::GetEnvironmentVariable($name) -ceq $before[$name]) "Caller variable $name not restored."}
    Write-Output 'PASS: in-process environment/location restoration.'
    Write-Output 'PASS: cooperative/forced/orphan ownership and launcher console/preflight tests.'
} finally {
    Remove-Item Env:DEV_FIXTURE_MODE -ErrorAction SilentlyContinue
    if (!$unrelated.HasExited) { $unrelated.Kill($true); $unrelated.WaitForExit() }
    $unrelated.Dispose()
}
