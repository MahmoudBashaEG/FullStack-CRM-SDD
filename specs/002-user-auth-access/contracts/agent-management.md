# Admin Agent Management Contract

All routes require validated live bearer plus shared AdminAgents policy; visitors401, active Customers/Agents403. No account existence check before policy. Existing safe ProblemDetails/no-store/strict JSON rules apply. Agent management is not Customer/Admin administration.

| Method/path | Request | Success |
|---|---|---|
| GET /api/agents?page=1&pageSize=25 | page>=1, size1–100; no other filter/query keys | 200 {items:AgentDto[],page,pageSize,totalCount} |
| POST /api/agents | {displayName,email,password} | 201 AgentDto; Location /api/agents/{userId} |
| GET /api/agents/{userId} | canonical Guid | 200 AgentDto (also Location dereference) |
| PATCH /api/agents/{userId} | {displayName,version} | 200 updated AgentDto |
| PUT /api/agents/{userId}/availability | {isActive:boolean,version} | 200 updated AgentDto |

AgentDto: {userId:Guid,displayName:string,email:string,role:"Agent",isActive:boolean,assignmentReferenceId:Guid,version:Base64 SQL rowversion}. Email is immutable but visible to the authorized Admin for identifying managed Agents. No credential hash/stamp/session list/token/version-secret returned. version is nonsecret edit concurrency metadata. Ordering displayName ascending then UserId; active and disabled Agents included, no portal/account search added. DTO limits/password semantics in authentication contract.

Version required for mutations, exactly valid base64 eight bytes; malformed/missing400 validation_failed, mismatched409 account_changed. Compare before no-op; unchanged valid input200 same version. Non-Agent/missing target404 agent_not_found after authorization; do not manage Admin/Customer through this surface. Safe syntactic errors400; duplicate normalized email409 agent_conflict with generic message and no credential values. Public registration remains generic400, not this privileged Admin diagnostic contract. Unknown fields including email/password/role/userId on updates rejected400; no DELETE/reset/role endpoints. Input role on creation rejected.

Creation atomically creates local account with Agent role and new linked reference; Identity save failure/reference failure rolls back both. Rename changes user and current reference label; old history remains untouched. Disable commits inactive+AuthVersion/SecurityStamp advancement+all family revocation+reference ineligibility together. Re-enable restores only future signin/assignment and requires fresh signin. Preserve reference Id and saved tickets at every step. Cancellation/failure cannot leave user/ref eligibility split. All database I/O uses the same Windows-authenticated connection.

GET detail is the minimal companion needed for Location/edit reload; no added broad account administration. Stale edit UI preserves user input and asks the Admin to reload/review; no overwrite retry. Concurrency tests cover simultaneous Admin changes, disable vs signin/refresh and reference assignment. Admin JWT role alone never creates an assignment reference.
