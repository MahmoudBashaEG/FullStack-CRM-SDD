$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. "$root/scripts/environment.ps1"
# The existing guarded SQL fixtures create/drop only their own disposable databases.
# No operator credentials, passwords in arguments or captured credential files.
$prior = $env:CRM_TEST_SQL_CONNECTION
try {
    $env:CRM_TEST_SQL_CONNECTION = 'Server=localhost;Database=CrmTicketsIntegration_' + [guid]::NewGuid().ToString('N') + ';Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
    dotnet test "$root/tests/backend/Crm.IntegrationTests" --no-build --filter 'FullyQualifiedName~AdminBootstrapCliTests' --logger 'console;verbosity=minimal'
    if ($LASTEXITCODE -ne 0) { throw 'Native bootstrap verification failed.' }
} finally { $env:CRM_TEST_SQL_CONNECTION = $prior }
