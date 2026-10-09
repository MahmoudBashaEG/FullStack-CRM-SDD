# Data Protection validation — T108–T110

Validated 2026-10-09 (Africa/Cairo). No signing keys, plaintext Data Protection keys, cookies, passwords or tokens are reproduced here.

## Configuration

The API uses framework Data Protection with stable application discriminator `Crm`, a durable Windows user-profile repository at `%LOCALAPPDATA%/ASP.NET/DataProtection-Keys`, and current-user Windows DPAPI encryption. An unavailable Windows profile fails setup rather than selecting an ephemeral repository. `DataProtection:KeyDirectory` can select an operator-managed/owned test directory; it still uses the same framework persistence and DPAPI protection. Default framework key lifetime/rollover remains unchanged. The API explicitly pins these existing framework conventions rather than assuming an arbitrary host will choose durable storage.

JWT signing is separate: the existing dedicated RSA certificate store remains authoritative. Refresh credentials are also separate: random opaque secrets have SHA-256 digests/session state in SQL, not Data Protection-encrypted refresh payloads. Data Protection protects antiforgery state across normal restarts.

Test repositories are exclusively owned `.local/dp-test-GUID` or `.local/auth-test-GUID` paths; the browser repository lives under its account-restricted owned `.local/e2e-*` directory. They are ignored by Git and removed only after owned host shutdown. Tests never delete or alter the real shared user key ring. Stored XML is checked for `encryptedSecret` and absence of plaintext master-key material, without publishing XML/content. Private signing certificates belong to their own fixture and are removed separately.

## Baseline / T108

`Framework_Windows_protected_keys_survive_two_owned_processes` launches the existing test-process executable twice under the same inherited Windows identity, with the same framework filesystem repository/DPAPI provider and discriminator. The first process protects a noncredential probe; the second unprotects it. Key filenames remain unchanged and encrypted storage is asserted.

The framework probe passed before production DataProtectionConfiguration wiring. No missing-behavior failure is invented for functioning framework behavior. An initial execution without the required SQL collection environment failed the existing guard; rerunning with a fresh disposable connection passed. This was a test setup error, not a Data Protection defect. Windows platform guards and the normal Host builder resolved compiler/analyzer checks without suppressing them.

## Real restart / T110

`Real_HTTPS_API_restart_accepts_saved_CSRF_and_refresh_then_revokes_access` runs actual API startup/controllers/middleware on Kestrel in two separate helper processes. The helper is test-only, references the real API and uses a private stdin control pipe to dispose its host normally; no public stop/seed endpoint or fake authentication scheme exists. Requests use normal trusted HTTPS with no certificate-validation callback bypass.

Sequence:

1. Migrate an exclusively owned SQL catalog explicitly and bootstrap its Admin through the actual command/standard Identity.
2. Start API process1 with an owned protected repository and signing certificate; obtain CSRF cookie/request token, sign in, retain refresh cookie and verify `/me`.
3. Close process1 normally, retaining SQL and the same key directory/signer/Windows identity.
4. Start process2; deliberately reuse the pre-restart CSRF cookie and request token, with no intervening `/csrf` remint.
5. Refresh successfully, observe rotated cookie, verify `/me` and protected reference data, sign out and verify the unexpired JWT now returns401.
6. Verify the same key files remain encrypted and that startup alone did not regenerate the key ring. Normal future framework rollover is not prohibited.

The initial restart fixture exposed an incorrect inferred API content root; explicitly selecting the real API directory fixed startup. Readiness and graceful shutdown assertions remained intact. Final DataProtectionPersistenceTests: **2/2 PASS**; both are included in the final **87/87** focused integration regression run.

## Reproduction

From the repository root, load `scripts/environment.ps1`, build `Crm.slnx` and `tests/scripts/fixtures/DevFixture`, and set `CRM_TEST_SQL_CONNECTION` to a fresh guarded `CrmTicketsIntegration_GUID` catalog using the existing local Integrated Security connection. Then run:

```powershell
dotnet test tests/backend/Crm.IntegrationTests --no-build --filter 'FullyQualifiedName~DataProtectionPersistenceTests' --logger 'console;verbosity=minimal'
```

The existing trusted development HTTPS certificate must be available; the test creates no trust-store entry/export and uses only its own temporary JWT signer. Missing SQL is a failure, not a skip. The normal helper process closes through its private control pipe and verifies exit0. No production infrastructure or schema migration was added.
