# Reference Provisioning Contract

Installation-only design, 2026-10-08. Supplies the spec's category/eligible-agent dependencies;
not a business administration module, sync engine or general importer. No HTTP endpoint/UI.

## Invocation and Ownership

Planned command from repository root:

```powershell
dotnet run --project src/backend/Crm.Api --no-launch-profile -- provision-ticket-references --input $env:CRM_REFERENCE_INPUT
```

API parses the command before starting its HTTP host, delegates to the Infrastructure
provisioner, prints sanitized result counts/errors and exits. Connection comes from user-secrets/
environment. Input path is local operator configuration; never log file contents or credentials.
Infrastructure owns the sole context/transaction. CLI uses the same Windows-authenticated
ConnectionStrings:Crm and Windows identity as API and EF tools, with no username/password or
identity switch. Validate integrated security before opening the connection. Runtime ticket
requests cannot invoke provisioning; only explicit CLI mode accepts the bounded input and exits
before HTTP. See [database-principals.md](database-principals.md) for the shared connection contract.

## JSON Input Version 1

Object properties: schemaVersion=1, categories (array, optional), agents (array, optional).
Omitted arrays mean no updates, not empty-source reconciliation. Unknown/duplicate properties
or schema versions reject the whole file. At least one nonempty array is required.

Category records require id (GUID), displayName (nonblank Unicode string), isAvailable (boolean).
Agent records require id (GUID), issuer (absolute HTTPS URI), subject (nonblank exact identity
string), displayName (nonblank Unicode string), isEligible (boolean). All fields are required;
no inferred identities, auto-generated IDs or default activation flags. Trim labels, preserve
issuer/subject exactly and compare identity keys ordinally. Display names need not be unique.
Duplicate IDs within a collection and duplicate agent issuer/subject pairs reject the file.
No password, token, role, queue permission or department/branch properties are supported.

Synthetic example:

```json
{
  "schemaVersion": 1,
  "categories": [
    { "id": "30000000-0000-0000-0000-000000000001", "displayName": "Test Category", "isAvailable": true }
  ],
  "agents": [
    { "id": "20000000-0000-0000-0000-000000000001", "issuer": "https://example.test", "subject": "agent-a", "displayName": "Test Agent", "isEligible": true }
  ]
}
```

## Validation, Upserts and Protected Fallback

1. Parse and validate the complete file before writing. Require applied schema/migrations and
   existing fallback ID 00000000-0000-0000-0000-000000000001, displayName Uncategorised,
   isAvailable=true. A missing/corrupt fallback is an error requiring migration/config repair.
2. Start one short transaction. Process categories then agents, each sorted by ID, with
   UPDLOCK/HOLDLOCK for rows/keys being upserted. Recheck DB uniqueness/invariants inside it.
3. New IDs insert; existing category IDs can update displayName/isAvailable. Existing agent IDs
   can update displayName/isEligible only; changed issuer/subject or rebinding an external key
   to a different ID rejects the whole transaction. Other omitted existing rows remain unchanged.
4. Uncategorised may be omitted or supplied with its exact protected label and isAvailable=true.
   Rename/deactivation rejects the entire input. No deletion property/mode exists; attempts
   reject as invalid input. Never remove omitted records or delete historical references.
5. Equal records are no-ops. Save all changed references once and commit atomically. Validation,
   uniqueness, lock or persistence failure rolls back every input change. Do not retry implicitly.
6. Print inserted/updated/unchanged counts only; no identity/file contents. Exit 0 on success,
   2 on validation/protected-reference errors, 3 on DB/transient/persistence failure. Exit on
   missing configuration/input; never start a web server or continue partially.

SQL CHECK enforces fallback availability; supported tooling never deletes rows. Separate
database privilege isolation is not implemented; direct SQL edits are outside supported
reference changes and are not prevented by process-specific credentials. The fallback stays protected while the feature uses it as default; changing
the default policy needs a separate specification/migration, not a switch in this command.

Provisioning never edits ticket IDs, saved references, versions, times or history. Becoming
inactive only prevents new selection, leaving existing references readable/retainable. It does
not grant queue access: issuer-issued claims determine authorization separately. Operator is
responsible for supplying approved identity/reference input; no external user creation occurs.

## Verification

Use an isolated SQL database to prove repeated input is a no-op; omissions preserve rows;
non-default deactivation leaves tickets/history/version unchanged; invalid input rolls back
all records; protected fallback rename/deactivation/deletion attempts fail; identity rebinding
fails; duplicate external identities fail. Test selection/inactivation racing with a ticket
mutation: whichever transaction acquires the reference lock first determines whether the new
selection succeeds, without partial ticket/history saves.
