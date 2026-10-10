# T115 / T116 normalization remediation

Date: 2026-10-10. Scope: T115 and T116 only.

## Result and canonical policy

Both tasks are complete under the user's explicit focused-unit/build validation scope. The common cause was JavaScript `trim()` differing from .NET `String.Trim()`: JavaScript removes U+FEFF (BOM) and retains U+0085 (NEL); .NET retains BOM and removes surrounding NEL. Signin could therefore submit a different identity, and name validation/submission could disagree with backend acceptance.

The shared Angular `trimAccountWhitespace` removes only surrounding Unicode White_Space, including NEL and excluding BOM. Email signup/create checks, signin submission, name validation, Customer signup, Agent creation and Agent rename reuse it. Internal characters remain unchanged. Backend parsing, NFC/case identity normalization, role/error/session behavior and password acceptance remain unchanged. Passwords are still passed exactly, without trimming or normalization. No backend production code or migration was needed.

## Regression coverage

Shared backend/frontend vectors cover ordinary surrounding whitespace, NEL, BOM-preserving addresses, quoted addresses containing spaces, invalid addresses, exact passwords, Unicode names at 200/201 scalar boundaries, blank names and malformed scalars. AuthService tests inspect the submitted identifier and stable current-user identity, including a distinct-identity response if canonical submission is wrong. Signin-page tests preserve the form identifier/password. Signup and Agent create/rename tests exercise accepted boundary names and reject blank/overlong/malformed names before submission. Shared-helper coverage preserves internal NEL and BOM.

Backend unit tests confirm the existing canonical policy and Customer/Agent/signin use-case submissions; backend production validation was already correct. No backend failing result is claimed.

## Executed verification

Commands were run from the repository root after `. ./scripts/environment.ps1`:

```powershell
npm --prefix src/frontend test -- --watch=false --include='**/account-validation.spec.ts' --include='**/auth.service.spec.ts' --include='**/signin.page.spec.ts' --include='**/signup.page.spec.ts' --include='**/agent-management.spec.ts'
dotnet test tests/backend/Crm.Domain.Tests --verbosity minimal
dotnet test tests/backend/Crm.Application.Tests --verbosity minimal
npm --prefix src/frontend test -- --watch=false
dotnet build Crm.slnx --no-restore --verbosity quiet
dotnet test tests/backend/Crm.ArchitectureTests --no-restore --verbosity minimal
npm --prefix src/frontend run build
```

| Check | Result |
|---|---|
| Focused Angular, before production correction | 10 failed, 80 passed; both defects reproduced |
| Focused Angular, after correction | 90 passed |
| Full Angular suite | 181 passed across 23 files |
| Domain unit suite | 46 passed |
| Application unit suite | 157 passed |
| Architecture suite | 7 passed |
| Backend solution build | Passed, zero warnings/errors |
| Angular production build | Passed |

Existing T112–T114 unit regressions remain included and passing in the full suites. Previous SQL/API/browser evidence remains historical, not a claim of re-execution. Per the user's explicit override, no SQL/API integration or Playwright tests were added or rerun solely for these fixes; no unexpected persistence/transport behavior change occurred. The tasks' wider SQL/browser verification wording was not rewritten.

## Files changed in this run

Production Angular:
- `src/frontend/src/app/core/auth/account-validation.ts`
- `src/frontend/src/app/core/auth/auth.service.ts`
- `src/frontend/src/app/features/auth/pages/signup.page.ts`
- `src/frontend/src/app/features/agents/pages/agent-create.page.ts`
- `src/frontend/src/app/features/agents/pages/agent-edit.page.ts`

Angular regression tests:
- `src/frontend/src/app/core/auth/account-validation.spec.ts`
- `src/frontend/src/app/core/auth/auth.service.spec.ts`
- `src/frontend/src/app/features/auth/pages/signin.page.spec.ts`
- `src/frontend/src/app/features/auth/pages/signup.page.spec.ts`
- `src/frontend/src/app/features/agents/pages/agent-management.spec.ts`

Backend regression tests:
- `tests/backend/Crm.Domain.Tests/Accounts/AccountRulesTests.cs`
- `tests/backend/Crm.Application.Tests/Accounts/AccountValidationTests.cs`
- `tests/backend/Crm.Application.Tests/Accounts/RegisterCustomerTests.cs`
- `tests/backend/Crm.Application.Tests/Accounts/AgentManagementTests.cs`
- `tests/backend/Crm.Application.Tests/Authentication/AuthUseCaseTests.cs`

Task/evidence updates: only T115/T116 completion markers in `specs/002-user-auth-access/tasks.md` and this new report.

No specification, approved plan/architecture, historical Feature001 validation or unrelated implementation was changed in this run. Feature001 T079 remains pending and T081 provisional. No design deviation, schema change, migration or blocker. Ready for a subsequent convergence review; convergence and commit were not executed.
