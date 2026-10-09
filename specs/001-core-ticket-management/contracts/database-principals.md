# Single Windows-Authenticated Database Connection

Revised project decision, 2026-10-09. This design uses local SQL Server and one existing
Windows identity for all database access. No separate SQL/database principals are created,
mapped, granted or verified by the project. This file retains its path for existing links;
it now specifies shared connection configuration and application boundaries, not principal ACLs.

## Configuration and Host Identity

API, EF migration tools and provisioning CLI load the same ConnectionStrings:Crm value and
run under the same Windows account in an environment/run. Integrated Security=True and
Trusted_Connection=True are equivalent choices. No database username/password is stored.

Local development example (adapt the local server/instance and catalog):

```text
Server=localhost;Database=CustomerSupportCrm;Integrated Security=True;Encrypt=True;TrustServerCertificate=True
```

TrustServerCertificate is a local self-signed SQL certificate setting; use normal certificate
validation when a trusted certificate is available. A normal configuration file may hold this
credential-free connection value; environment ConnectionStrings__Crm may override it. Do not
add migration/provisioning connection keys or SQL authentication credentials. The existing
Windows account's required SQL access is an environment prerequisite, not an app-created grant.

Infrastructure validates with SqlConnectionStringBuilder: IntegratedSecurity=true, nonempty
local DataSource and InitialCatalog, no User ID/Password keys (even empty), and no conflicting
Authentication mode. Do not set SqlCredential/AccessToken, EXECUTE AS or staff impersonation.
Windows host identity is independent of the JWT-authenticated staff actor recorded in history.
The design-time factory uses the same validated configuration without starting HTTP. CLI mode
exits before HTTP startup and does not automatically migrate or provision omitted inputs.
Normal HTTP startup awaits existing pending EF migrations only in Development; Production,
Staging, Testing and other environments retain explicit migration application. This uses the
same connection/Windows identity, not separate migration credentials. See the
[startup design](../plan.md#local-database-migration-startup).

Automatic application means MigrateAsync over the source-controlled migrations included in the
build, never EnsureCreated or startup migration generation. Model changes still need an explicitly
generated/reviewed/versioned migration. Development migration failure prevents HTTP readiness.
No application startup performs reference provisioning beyond data explicitly defined in migrations.

## Supported Application Boundaries and Trade-off

- Shared-queue JWT authorization still protects every ticket/history/reference endpoint.
- Ticket history is append-only and immutable through supported application operations.
  Save only new entries atomically with actual ticket changes; no history update/delete use case.
- Reference CLI accepts only the documented category/agent upserts. No delete/reconciliation,
  identity/password/permission management, ticket edits or history edits.
- Uncategorised remains protected by the availability CHECK and supported provisioning validation.
- Unsupported ticket/history/reference deletes or administration operations have no app routes/UI.

Database least-privilege separation is intentionally not implemented. The single Windows identity
may possess the schema/DML rights needed for migration and provisioning while running the API.
Application process/use-case boundaries do not prevent direct SQL or bypassed-code modifications.
No separate-credential or database-denied-DML guarantee is claimed. No principal creation scripts,
per-process grant matrix or effective-denial test suite is part of this feature.

## Verification and Test Isolation

In each host, verify the shared connection parses as IntegratedSecurity=true with no SQL user/
password keys. Compare WindowsIdentity.GetCurrent().Name and SQL ORIGINAL_LOGIN()/SUSER_SNAME()
across API, design-time EF tooling and CLI; all must refer to the intended same Windows account,
local server and catalog. Report agreement/configuration failures without exposing connection
strings or introducing SQL credentials. The identity must already be able to perform the required
operations; do not automatically create a login/principal or switch to password authentication.

Read-only SQL probe for local verification:

```sql
SELECT ORIGINAL_LOGIN() AS original_login, SUSER_SNAME() AS current_login, DB_NAME() AS database_name;
```

Integration/E2E tests use the same local Windows account. Change only InitialCatalog to the
approved disposable CrmTicketsIntegration_/CrmTicketsE2E_ database. Within that run API, EF,
CLI and fixture setup share the identical test connection; no second admin/runtime credential.
Validate the actual local server/catalog and exclusive fixture ownership before reset and
exclude the configured development database. Never rely on a name prefix alone for cleanup.

Test actual create/update/history/migration/upsert behavior and application-level rejection
of unknown/delete/admin inputs, protected fallback changes and history edit/delete routes.
Do not expect SQL DELETE or DDL to fail because of process-specific permission denial.
Ephemeral JWT signing keys remain isolated test authentication material, unrelated to DB login.
