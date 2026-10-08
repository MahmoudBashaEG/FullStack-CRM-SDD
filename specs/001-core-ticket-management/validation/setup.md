# Setup validation — T001–T006

2026-10-08. Scope: initialize projects; no feature acceptance claimed.

- .NET SDK 10.0.400, installed runtime 10.0.11; target net10.0.
- EF/ASP.NET NuGet baseline 10.0.12; MediatR 12.5.0.
- Node 24.20.0, npm 11.19.0; Angular framework 22.2.1, CLI/build 22.2.2, TypeScript 6.0.3.
- `dotnet restore Crm.slnx`: passed; package lockfiles generated.
- `dotnet tool restore`: passed, dotnet-ef 10.0.12.
- `dotnet build Crm.slnx --no-restore`: passed, zero warnings/errors.
- `npm install --prefix src/frontend`: passed; exact manifest versions and lockfile generated.
- `npm run build --prefix src/frontend`: passed.
- `npm audit --prefix src/frontend --omit=dev`: zero vulnerabilities.
- Initial NuGet transitive vulnerability review: zero known vulnerabilities.

The installed local SQL Server reports version 17.0.1000.7. Retain SQL Server 2022-compatible model semantics. Existing Windows authentication connected successfully; no accounts/grants were created. Disposable integration/browser catalogs will be uniquely created and owned by their fixtures, never reset by name prefix alone.

Execution requires sandbox escalation for NuGet HTTPS, Windows SQL authentication and build subprocesses. Caches live under ignored `.local/`, configured by `scripts/environment.ps1`.

An initial Angular pin used nonexistent framework 22.2.2; registry verification corrected it to 22.2.1 before successful install. No peer-dependency bypass was used.
