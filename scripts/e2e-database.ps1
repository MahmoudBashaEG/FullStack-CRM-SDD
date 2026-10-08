param([ValidateSet('Prepare','SeedTracking','SeedHistory','Cleanup')][string]$Mode, [Parameter(Mandatory)][string]$Owner)
$ErrorActionPreference = 'Stop'
if ($Owner -notmatch '^[a-f0-9]{32}$') { throw 'Invalid fixture ownership token.' }
$taskSettings = [System.Data.Common.DbConnectionStringBuilder]::new()
$taskSettings.set_ConnectionString($env:CRM_E2E_SQL_CONNECTION)
$taskServer = [string]$taskSettings['Server']
$taskCatalog = [string]$taskSettings['Database']
if ($taskServer -notmatch '^(localhost|\.|127\.0\.0\.1|\(local\))(\\[A-Za-z0-9_]+)?(,\d+)?$' -or $taskCatalog -notmatch '^CrmTicketsE2E_[a-f0-9]{32}$' -or $taskSettings['Integrated Security'] -ne 'True' -or $taskSettings.ContainsKey('User ID') -or $taskSettings.ContainsKey('Password')) { throw 'Unsafe test database configuration.' }
function Invoke-FixtureSql([string]$Catalog, [string]$Query) {
    $taskOutput = & sqlcmd -S $taskServer -E -C -b -l 10 -d $Catalog -h -1 -W -Q "SET NOCOUNT ON; $Query"
    if ($LASTEXITCODE -ne 0) { throw 'SQL fixture command failed.' }
    return ($taskOutput -join "`n").Trim()
}
$taskMachine = Invoke-FixtureSql 'master' "SELECT CONVERT(nvarchar(128), SERVERPROPERTY('MachineName'))"
if ($taskMachine -ine $env:COMPUTERNAME) { throw 'Actual SQL server is not this machine.' }
if ($Mode -eq 'Prepare') {
    Invoke-FixtureSql 'master' "IF DB_ID(N'$taskCatalog') IS NOT NULL THROW 50001, 'Existing catalog cannot be reset', 1; CREATE DATABASE [$taskCatalog]" | Out-Null
    Invoke-FixtureSql $taskCatalog "EXEC sys.sp_addextendedproperty @name=N'CrmFixtureOwner', @value=N'$Owner'" | Out-Null
} else {
    $taskActualOwner = Invoke-FixtureSql $taskCatalog "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'CrmFixtureOwner' AND DB_NAME()=N'$taskCatalog'"
    if ($taskActualOwner -ne $Owner) { throw 'Fixture ownership mismatch; database retained.' }
    if ($Mode -in 'SeedTracking','SeedHistory') {
        $taskFixtureName = if ($Mode -eq 'SeedTracking') { 'TrackingTickets.sql' } else { 'FullTicketHistory.sql' }
        $taskFixturePath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "../tests/backend/Crm.IntegrationTests/Fixtures/$taskFixtureName")).Path
        & sqlcmd -S $taskServer -E -C -I -f 65001 -b -l 10 -d $taskCatalog -i $taskFixturePath | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Synthetic fixture preparation failed.' }
    } else {
        Invoke-FixtureSql 'master' "ALTER DATABASE [$taskCatalog] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$taskCatalog]" | Out-Null
    }
}
