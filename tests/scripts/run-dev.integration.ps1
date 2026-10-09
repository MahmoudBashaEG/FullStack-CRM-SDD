param([switch]$KeepRunning,[switch]$InitialSchema)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. "$root/scripts/environment.ps1"
if (!('Crm.Dev.Child' -as [type])) { Add-Type -Path "$root/scripts/DevProcess.cs" }
function Assert([bool]$condition,[string]$message) { if (!$condition) { throw $message } }
function Status([string]$url,[string]$token = '') {
    $headers = if ($token) { @{ Authorization="Bearer $token" } } else { @{} }
    return (Invoke-WebRequest $url -Headers $headers -SkipHttpErrorCheck -TimeoutSec 3).StatusCode
}
function Wait-Ready {
    $clock=[Diagnostics.Stopwatch]::StartNew()
    while ($clock.Elapsed.TotalSeconds -lt 120) {
        if ($script:launcher.HasExited) { throw 'Real launcher exited before readiness.' }
        try { if ((Status 'https://localhost:7043/api/ticket-reference-data/categories') -eq 401 -and (Status 'https://localhost:4200/') -eq 200) { return } } catch { }
        Start-Sleep -Milliseconds 200
    }
    throw 'Real services not available in 120 seconds.'
}
function Request([string]$path,[string]$method='GET',$body=$null) {
    $parameters=@{ Uri="https://localhost:4200/api/$path";Method=$method;Headers=@{Authorization="Bearer $($script:tokens.staff)"};TimeoutSec=15 }
    if ($null -ne $body) { $parameters.Body=($body|ConvertTo-Json -Depth 12 -Compress);$parameters.ContentType='application/json' }
    Invoke-RestMethod @parameters
}
function Sql([string]$query) {
    $result=& sqlcmd -S localhost -E -C -b -d $script:catalog -h -1 -W -Q "SET NOCOUNT ON; $query"
    Assert ($LASTEXITCODE -eq 0) 'Owned SQL observation failed.'
    ($result -join "`n").Trim()
}
$envKeys=@('ConnectionStrings__Crm','CRM_E2E_SQL_CONNECTION','Authentication__Authority','Authentication__Audience','DOTNET_ENVIRONMENT','ASPNETCORE_ENVIRONMENT','ASPNETCORE_URLS','NODE_EXTRA_CA_CERTS','NODE_USE_SYSTEM_CA','NODE_OPTIONS','NG_CLI_ANALYTICS')
$previous=@{};foreach($key in $envKeys){$previous[$key]=[Environment]::GetEnvironmentVariable($key)}
$owner=[guid]::NewGuid().ToString('N');$script:catalog='CrmTicketsE2E_'+[guid]::NewGuid().ToString('N')
$directory=Join-Path $root ".local/dev-integration-$owner"
New-Item -ItemType Directory -Path $directory | Out-Null
$issuer=$null;$script:launcher=$null;$prepared=$false
try {
    # Public PEM only; the key remains in the existing trusted Windows store for the issuer/API.
    $env:NODE_EXTRA_CA_CERTS=Join-Path $root '.local/dev-certs/localhost.pem'
    Assert (Test-Path $env:NODE_EXTRA_CA_CERTS) 'Complete the approved local certificate export setup first.'
    $fixture="$PSScriptRoot/fixtures/DevFixture/bin/Debug/net10.0/DevFixture.exe"
    $tokenFile=Join-Path $directory 'tokens.json'
    $issuer=[Crm.Dev.Child]::Start('test issuer',$fixture,[string[]]@('issuer',$tokenFile),$root)
    $clock=[Diagnostics.Stopwatch]::StartNew();while(!(Test-Path $tokenFile) -and $clock.Elapsed.TotalSeconds -lt 10){Start-Sleep -Milliseconds 100}
    Assert (Test-Path $tokenFile) 'Issuer did not issue private fixture tokens.'
    $script:tokens=Get-Content $tokenFile -Raw | ConvertFrom-Json
    $env:Authentication__Authority='https://localhost:7443';$env:Authentication__Audience='crm-dev-fixture'
    $env:CRM_E2E_SQL_CONNECTION="Server=localhost;Database=$script:catalog;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
    $env:ConnectionStrings__Crm=$env:CRM_E2E_SQL_CONNECTION
    & "$root/scripts/e2e-database.ps1" -Mode Prepare -Owner $owner;$prepared=$true
    if($InitialSchema){
        dotnet ef database update 20261008164804_InitialTicketStorage --no-build --project "$root/src/backend/Crm.Infrastructure" --startup-project "$root/src/backend/Crm.Api"
        Assert ($LASTEXITCODE -eq 0) 'Owned older-schema fixture preparation failed.'
        Assert ((Sql 'SELECT COUNT(*) FROM dbo.__EFMigrationsHistory') -eq '1') 'Fixture silently pre-applied pending migrations.'
    }
    $shell=(Get-Command pwsh.exe | Select-Object -First 1).Source
    $script:launcher=[Crm.Dev.Child]::Start('root launcher',$shell,[string[]]@('-NoProfile','-Command','& ./scripts/run-dev.ps1'),$root)
    Wait-Ready
    Assert ((Sql 'SELECT COUNT(*) FROM dbo.__EFMigrationsHistory') -eq '2') 'Fresh Development startup failed to apply both migrations.'
    Assert ((Sql "SELECT COUNT(*) FROM sys.objects WHERE name='TicketSubjectContainsLiteral'") -eq '1') 'SQL-only search migration missing.'
    Assert ((Status 'https://localhost:4200/api/ticket-reference-data/categories') -eq 401) 'Nested proxy did not preserve unauthenticated denial.'
    foreach($tokenName in @('expired','wrongAudience')) { Assert ((Status 'https://localhost:4200/api/ticket-reference-data/categories' $tokens.$tokenName) -eq 401) 'Invalid JWT accepted through normal middleware.' }
    Assert ((Status 'https://localhost:4200/api/ticket-reference-data/categories' $tokens.denied) -eq 403) 'Denied JWT received queue access.'
    $categories=Request 'ticket-reference-data/categories'
    Assert (@($categories|Where-Object displayName -eq 'Uncategorised').Count -eq 1) 'Authorized fallback read failed.'
    Assert ((Status 'https://localhost:4200/api/tickets?unknown=1' $tokens.staff) -eq 400) 'Proxy did not preserve path/query.'
    $apiDocs=Invoke-WebRequest 'https://localhost:4200/api-docs'
    Assert ($apiDocs.Content -match '<html') '/api-docs was incorrectly proxied.'
    $agent=[guid]::NewGuid().ToString()
    $inputFile=Join-Path $directory 'references.json'
    @{schemaVersion=1;agents=@(@{id=$agent;issuer='https://localhost:7443';subject='fixture-agent';displayName='Synthetic Agent';isEligible=$true})}|ConvertTo-Json -Depth 5|Set-Content $inputFile
    $env:DOTNET_ENVIRONMENT=$env:ASPNETCORE_ENVIRONMENT='Development'
    dotnet run --no-build --no-launch-profile --project "$root/src/backend/Crm.Api" -- provision-ticket-references --input $inputFile
    Assert ($LASTEXITCODE -eq 0) 'Explicit post-start reference setup failed.'
    $ticket=Request 'tickets' 'POST' @{subject='Launcher integration وصف';description='Normal JWT through the frontend proxy'}
    $reference=$ticket.reference
    $ticket=Request "tickets/$reference/details" 'PATCH' @{expectedVersion=$ticket.editVersion;subject='Edited launcher integration'}
    $ticket=Request "tickets/$reference/assignment" 'PUT' @{expectedVersion=$ticket.editVersion;agentId=$agent}
    foreach($status in @('InProgress','Resolved','Closed','Open')) { $ticket=Request "tickets/$reference/status" 'PUT' @{expectedVersion=$ticket.editVersion;status=$status} }
    $detail=Request "tickets/$reference";$history=Request "tickets/$reference/history"
    Assert ($detail.reference -eq $reference -and $detail.status -eq 'Open' -and $detail.assignedAgent.id -eq $agent) 'Lifecycle/assignment preservation failed.'
    foreach($kind in @('Created','DetailsChanged','AssignmentChanged','StatusChanged')) { Assert (@($history.items|Where-Object kind -eq $kind).Count -gt 0) 'Missing readable history kind.' }
    $migrationIDs=Sql 'SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId'
    $historyCount=Sql 'SELECT COUNT(*) FROM dbo.TicketHistoryEntries'
    Write-Output 'PASS real root command, trusted HTTPS proxy, normal JWT denials, fallback and full API lifecycle/history.'
    if ($KeepRunning) {
        Write-Output 'MANUAL VERIFICATION READY: frontend https://localhost:4200 ; API https://localhost:7043. Private fixture issuer is not SPA PKCE sign-in.'
        while(!$launcher.HasExited){Start-Sleep -Seconds 1}
    } else {
        # Trigger a real rebuild/restart through an isolated source edit, restored byte-for-byte.
        $source=Join-Path $root 'src/backend/Crm.Api/Crm.Api.csproj';$original=[IO.File]::ReadAllBytes($source)
        $apiProcessBefore=(Get-NetTCPConnection -State Listen -LocalPort 7043|Select-Object -First 1).OwningProcess
        try {
            [IO.File]::WriteAllText($source,[IO.File]::ReadAllText($source).Replace('</Project>', '<PropertyGroup><DefineConstants>$(DefineConstants);CRM_DEV_WATCH_PROBE</DefineConstants></PropertyGroup></Project>'))
            $reloadClock=[Diagnostics.Stopwatch]::StartNew();$changed=$false
            while($reloadClock.Elapsed.TotalSeconds -lt 60){
                $currentApi=Get-NetTCPConnection -State Listen -LocalPort 7043 -ErrorAction SilentlyContinue|Select-Object -First 1
                if($currentApi -and $currentApi.OwningProcess -ne $apiProcessBefore){$changed=$true;break}
                Start-Sleep -Milliseconds 250
            }
            Assert $changed 'dotnet watch did not actually restart the API after project change.'
            [IO.File]::WriteAllBytes($source,$original)
            Start-Sleep -Seconds 8
            Wait-Ready
        } finally {[IO.File]::WriteAllBytes($source,$original)}
        & node "$root/tests/scripts/fixtures/browser-observations.mjs"
        Assert ($LASTEXITCODE -eq 0) 'Normal browser/Angular live reload checks failed.'
        [Crm.Dev.Child]::StopAll([Crm.Dev.Child[]]@($launcher),5000)|Write-Output;$launcher.Dispose();$launcher=$null
        $script:launcher=[Crm.Dev.Child]::Start('root launcher',$shell,[string[]]@('-NoProfile','-Command','& ./scripts/run-dev.ps1'),$root)
        Wait-Ready
        Assert ((Sql 'SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId') -eq $migrationIDs) 'Repeat start changed migration history.'
        Assert ((Sql 'SELECT COUNT(*) FROM dbo.TicketHistoryEntries') -eq $historyCount) 'Restart created ticket activity.'
        Assert ((Request "tickets/$reference").reference -eq $reference) 'Saved ticket lost on restart.'
        Write-Output 'PASS real watch change and repeated launcher startup: migrations/data unchanged.'
        & node "$root/tests/scripts/dev-proxy.tests.mjs"
        Assert ($LASTEXITCODE -eq 0) 'Real proxy observations failed.'
        [Crm.Dev.Child]::StopAll([Crm.Dev.Child[]]@($launcher),5000)|Write-Output;$launcher.Dispose();$launcher=$null
        # Negative trust case: keep the actual Angular proxy, but remove only its child process's trusted CA.
        $env:ASPNETCORE_URLS='https://localhost:7043';$env:NG_CLI_ANALYTICS='false'
        $stub=[Crm.Dev.Child]::Start('TLS fixture backend',$fixture,[string[]]@('watch','--non-interactive','run','--no-launch-profile'),$root)
        $negativeFrontend=$null
        try {
            $publicCert=Join-Path $root '.local/dev-certs/localhost.pem';$privateKey=Join-Path $root '.local/dev-certs/localhost.key'
            $emptyCa=Join-Path $directory 'empty-ca.pem';[IO.File]::WriteAllText($emptyCa,'')
            $env:NODE_EXTRA_CA_CERTS=$emptyCa;$env:NODE_USE_SYSTEM_CA='0';$env:NODE_OPTIONS=$null
            $node=(Get-Command node.exe|Select-Object -First 1).Source;$npm=(Get-Command npm.cmd|Select-Object -First 1).Source
            $negativeFrontend=[Crm.Dev.Child]::Start('Angular negative trust',$node,[string[]]@((Join-Path (Split-Path $npm) 'node_modules/npm/bin/npm-cli.js'),'run','start','--prefix',(Join-Path $root 'src/frontend'),'--','--configuration','development','--port','4200','--ssl','--host','localhost','--ssl-cert',$publicCert,'--ssl-key',$privateKey),$root)
            $env:NODE_EXTRA_CA_CERTS=$publicCert
            $clock.Restart();$available=$false
            while($clock.Elapsed.TotalSeconds -lt 30){try{if((Status 'https://localhost:4200/') -eq 200){$available=$true;break}}catch{};Start-Sleep -Milliseconds 200}
            Assert $available 'TLS negative frontend did not start.'
            & node "$root/tests/scripts/dev-proxy.tests.mjs" --tls-negative
            Assert ($LASTEXITCODE -eq 0) 'Proxy silently accepted untrusted backend TLS.'
        } finally {
            if($negativeFrontend){[Crm.Dev.Child]::StopAll([Crm.Dev.Child[]]@($negativeFrontend),5000)|Write-Output;$negativeFrontend.Dispose()}
            [Crm.Dev.Child]::StopAll([Crm.Dev.Child[]]@($stub),5000)|Write-Output;$stub.Dispose()
        }
    }
} finally {
    if($launcher){[Crm.Dev.Child]::StopAll([Crm.Dev.Child[]]@($launcher),5000)|Write-Output;$launcher.Dispose()}
    if($issuer){[Crm.Dev.Child]::StopAll([Crm.Dev.Child[]]@($issuer),5000)|Write-Output;$issuer.Dispose()}
    if($prepared){& "$root/scripts/e2e-database.ps1" -Mode Cleanup -Owner $owner}
    foreach($key in $envKeys){if($null -eq $previous[$key]){Remove-Item -LiteralPath "Env:$key" -ErrorAction SilentlyContinue}else{[Environment]::SetEnvironmentVariable($key,[string]$previous[$key])}}
    if([IO.Path]::GetFullPath($directory).StartsWith([IO.Path]::GetFullPath((Join-Path $root '.local'))+[IO.Path]::DirectorySeparatorChar)){Remove-Item -LiteralPath $directory -Recurse -Force}
}
