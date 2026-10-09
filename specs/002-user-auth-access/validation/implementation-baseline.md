# Feature002 T001–T027 baseline

2026-10-09. Scope: foundation only. Existing Feature001 artifacts are hashed in feature001-artifact-baseline.json and must remain unchanged. T079 stays pending/T081 provisional. Feature002 supports one active browser tab/session; no Web Locks/BroadcastChannel dependency. Existing workspace had only Feature002 planning/shared architecture untracked files. Ignored .local, build output and private certificate formats remain excluded. Builds/tests use existing environment.ps1; real SQL tests require exclusively owned CrmTicketsIntegration_GUID catalogs under Windows Integrated Security. Fixed-port developer processes are never taken over or terminated.

Runtime pins: SDK10.0.400; net10.0; EF/JwtBearer10.0.12; MediatR12.5.0; Angular22.2.x; TypeScript6.0.3; Node24/npm11. Baseline commands/results follow after actual execution. No participant evidence is generated.

Observed baseline: backend build0warnings/0errors;304backend tests pass;71frontend tests pass. Sandbox-only Angular file access denial resolved by approved normal-access rerun. New foundation results are in foundation.md; historical hashes remain intact.
