# Story 08 — Customer Notes, Interaction History & Attachments

## Prerequisites

- **Story 07 completed:** [../agent-dashboard/07-story-agent-productivity-collaboration.md](../agent-dashboard/07-story-agent-productivity-collaboration.md) (`DASH-002`, committed as `b9eec58`; working tree clean apart from the untracked `.squad/stories/customer-management/CUST-002/` intake). Relevant precedent: append-only `TicketInternalMessage` (`bigint IDENTITY`, author joined at read time, newest-first "Load older" paging), `CollaborationContracts.cs` (several contracts in one file), `StaffActor`, `ForbiddenException`. **This story uses none of DASH-002's concurrency machinery** (no ETag / `If-Match`, no `ITransactionalCommand`, no row locks). The intake says so explicitly (§6).
- **Story 03 completed:** [03-story-customer-profiles-contact-management.md](03-story-customer-profiles-contact-management.md) (`CUST-001`, `0200e16`): `Crm.Domain.Customers.Customer` / `Customers.Id` is the key that every new row references. Also `ICustomerRepository`, `PagedResult<T>` / `Paging`, `PagedResponse<T>`, `AuthorizationPolicies.Staff`, `CustomersController`, Angular `features/customers/` (list / create / detail pages, `CustomersApi`), and `docs/customer-management.md`.
- **Stories 04–05 completed:** [../ticket-management/04-story-ticket-creation-management.md](../ticket-management/04-story-ticket-creation-management.md), [../ticket-management/05-story-ticket-workflow-assignment-history.md](../ticket-management/05-story-ticket-workflow-assignment-history.md): `GET /api/v1/tickets?customerId=` already exists and is scoped in SQL by `TicketRepository.Scope`. **Related Tickets reuse it unchanged.**
- **Stories 01–02 completed:** Mediator/CQRS pipeline (`ValidationBehavior`, `GlobalExceptionHandler`), JWT `sub`/`role`, the `OnTokenValidated` active-account check, `User.GetUserId()`, `roleGuard`, and the auth interceptor.
- Applied migrations (verified in `backend/src/Crm.Infrastructure/Persistence/Migrations/`): `20261014234527_InitialCreate`, `20261015011303_AddIdentityAndRefreshSessions`, `20261017183245_AddCustomers`, `20261017194039_AddTickets`, `20261017203222_AddTicketWorkflowAndActivity`, `20261017232626_AddAgentProductivityAndCollaboration`. **This story adds exactly one additive migration, `AddCustomerNotesInteractionsAttachments`, and edits none of the existing ones.**
- Tooling unchanged: .NET SDK 10, EF Core 10, Mediator (source-generated), FluentValidation, xUnit 2.9.3 (`backend/tests/Crm.UnitTests/Crm.UnitTests.csproj` references only `Crm.Application`), Angular / Material 22, and Vitest via `ng test`. **No new NuGet or npm packages.**
- Intake: `.squad/stories/customer-management/CUST-002/intake.md` (`attachments/` is empty). The original *Customer Support CRM — Core Features* document is **not** in the repository (`git ls-files` has no `.pdf` / `.docx`). The intake text, including approved decisions **C1–C5**, is therefore the only requirements source. Do not invent requirements beyond it.
- Shared contracts touched (additive only): `CrmDbContext` (+3 `DbSet`s), `Infrastructure/DependencyInjection.cs` (+1 repository), the Angular `CustomerDetailPage` template (+4 cards after the existing two), and docs. **No change** to `Customer`, `CustomerDetails`, `CustomerResponse`, `ICustomerRepository`, `CustomersController`, `TicketsController`, `TicketRepository`, `ITicketRepository`, `GlobalExceptionHandler`, `EfUnitOfWork`, `AuthorizationPolicies`, `app.routes.ts`, `NAV_ITEMS`, or any existing migration or test.

---

## Story Goal

Complete the last two Customer Management items from the core-features list ("Interaction history", "Notes and attachments") inside the existing **staff Customer Details** page:

1. **Notes:** SuperAdmin and Agents add plain-text internal notes (1–2,000 characters, trimmed) on any customer. Notes are listed newest first with author and UTC time, with "Load older" paging. They are **append-only**: no edit or delete API or UI (C2 Option A).
2. **Interactions:** staff manually log a `PhoneCall` / `Meeting` / `Other` interaction with a summary (1–500 characters) and an occurred-at time (defaults to now; past allowed; not in the future). The list is newest first by occurred-at and shows type, summary, time and recorder. Interactions are **append-only** (C2).
3. **Related Tickets:** a section lists the customer's tickets that the **current** staff member can already open: SuperAdmin sees all, an Agent sees only tickets assigned to them. It uses the existing `GET /api/v1/tickets?customerId=` and links to `/tickets/:id`. No new ticket access path is added.
4. **Attachments:** staff upload one file at a time (≤ 5 MB; PDF, PNG, JPG/JPEG, TXT, DOCX; checked by extension **and** file signature), list metadata (name, size, uploader, UTC time) and download through an authenticated endpoint with `Content-Disposition: attachment`. Bytes are stored in SQL Server in `CustomerAttachments.Content` and are **never** part of any JSON or metadata query. Attachments cannot be deleted in this story.
5. **Customers never see any of this:** every new endpoint carries the `Staff` policy, so Customer-role tokens get **403**. The Customer self-profile (`/profile`, `/api/v1/me/customer-profile`) is untouched.
6. Exactly **one** new backend and **one** new frontend happy-path unit test. Manual API / SQL / browser verification. `docs/customer-management.md` extended, plus small updates to the README, identity and API docs.

**Not in scope** (do **not** build): edit, delete or pin notes or interactions; delete, preview, version or bulk-upload attachments; customer-uploaded files or any customer-visible history; Email / WhatsApp / SMS / Live Chat / web-form ingestion or sending (the future Communication Channels module); a unified activity feed or event platform; notifications or mentions; antivirus or external storage; changes to ticket visibility, `TicketActivity` or DASH-002 discussions; new test frameworks; integration or E2E suites.

### Recorded decisions

| Concern | Decision |
|---|---|
| Naming | Domain types `CustomerNote`, `CustomerInteraction`, `CustomerInteractionType`, `CustomerAttachment` in the existing namespace `Crm.Domain.Customers`. Tables `CustomerNotes`, `CustomerInteractions`, `CustomerAttachments`. Application use cases sit under `Crm.Application/Customers/<UseCase>/`, next to CUST-001's. |
| Repository | New `ICustomerRecordRepository` / `CustomerRecordRepository`. **`ICustomerRepository` is not extended**, so the hand-written `FakeCustomerRepository` in `CreateCustomerCommandHandlerTests.cs` (lines 36–46) and in `CreateTicketCommandHandlerTests.cs` (line 69) keep compiling. |
| IDs | Notes and interactions: `long`, store-generated `bigint IDENTITY` (as `TicketInternalMessageConfiguration.cs` line 15). Append-only, and insertion order breaks ties. Attachments: `Guid.CreateVersion7()` (as `CreateCustomerCommand.cs` line 21), because the id appears in the download URL. |
| Actor | Only the author / recorder / uploader id is needed. Controllers pass `User.GetUserId()` (JWT `sub`; precedent: `CollaborationsController.cs` line 34) as `Guid ActorUserId`. The id is never read from the request body. The `Staff` policy plus the `OnTokenValidated` active-account check guarantee it is an active SuperAdmin or Agent. FKs to `AspNetUsers` (`Restrict`) guarantee it exists. |
| Routes (API) | New `CustomerRecordsController` at `[Route(ApiRoutes.V1 + "/customers/{customerId:guid}")]` with `[Authorize(Policy = AuthorizationPolicies.Staff)]`: `GET`/`POST notes`, `GET`/`POST interactions`, `GET`/`POST attachments`, `GET attachments/{attachmentId:guid}/content`. No `PUT`/`DELETE` routes exist (C2, C4). Related tickets: existing `GET /api/v1/tickets?customerId=` (**no backend change**). |
| 404 vs 400 vs 403 | Unknown `customerId` (route) ⇒ **404** `NotFoundException("Customer '{id}' was not found.")`, same text as `GetCustomerQuery.cs` line 13. Unknown attachment id, or an attachment of another customer ⇒ **404**. Invalid body or file ⇒ **400** `ValidationProblemDetails`. Customer role ⇒ **403** (policy). No `ForbiddenException` is needed. |
| Transactions | Every command is a **single save** through `IUnitOfWork.SaveChangesAsync` (as `CreateCustomerCommand.cs` lines 29–30). Not `ITransactionalCommand`. Customer existence is checked before the insert. Customers cannot be deleted (CUST-001 §7), and the FK is the hard guarantee. |
| Paging | All three lists use `page` (1…`Paging.MaxPage`), `pageSize` (≥ 1, clamped to `Paging.MaxPageSize`), and return `PagedResponse<T>`. Order: notes `CreatedAtUtc DESC, Id DESC`; interactions `OccurredAtUtc DESC, Id DESC`; attachments `UploadedAtUtc DESC, Id DESC`. UI page size is 10 with "Load older"/"Load more" (pattern from `ticket-discussion.ts`). |
| Author names | Joined from `dbContext.Users` at read time (`FirstName + " " + LastName`), never stored. Precedent: `CollaborationRepository.cs` lines 57–62. |
| Interaction time | `OccurredAtUtc` is optional in the request; null ⇒ server `now` (`TimeProvider`). It is rejected when later than `now + 5 min` (clock-skew tolerance, `CustomerRecordRules.InteractionFutureTolerance`) ⇒ 400 `occurredAtUtc` "The interaction time cannot be in the future.", and rejected before `2000-01-01` ⇒ 400 "Enter a valid interaction time.". `CreatedAtUtc` is always server `now`. |
| Interaction type | `CustomerInteractionType { PhoneCall = 0, Meeting = 1, Other = 2 }`, stored as the member name (`HasConversion<string>().HasMaxLength(20)`, as `TicketConfiguration.cs` lines 20–22). JSON carries `"PhoneCall"` (global `JsonStringEnumConverter`, `Crm.Api/DependencyInjection.cs` line 17). The UI label is "Phone Call". It is nullable in the command, so a missing field fails validation instead of defaulting (precedent: `CreateTicketCommand.cs` line 13 comment). |
| Attachment storage (C4) | One table, `CustomerAttachments`, with `Content varbinary(max)`. Metadata reads **project** without `Content`. Only `FindAttachmentFileAsync` selects it. The `Customers` table and `CustomerDetails` / `CustomerResponse` are unchanged, so customer JSON never carries bytes. |
| Attachment validation | Server-side only rules in `CustomerAttachmentRules.Check(fileName, content)`, reported as a single 400 key **`file`**: non-empty; ≤ **5 MB** (5,242,880 bytes); sanitized name non-empty and ≤ 255 characters; extension in `.pdf .png .jpg .jpeg .txt .docx` (case-insensitive); content starts with the type's signature (PDF `%PDF-`, PNG `89 50 4E 47 0D 0A 1A 0A`, JPEG `FF D8 FF`, DOCX zip `50 4B 03 04`); TXT must contain no `0x00` byte. The client's `Content-Type` is **ignored**. The stored content type comes from the extension allow-list. |
| File names | `SanitizeFileName`: take the part after the last `/` or `\`, remove control characters, trim. The result is display metadata only. Nothing is ever written to the file system. Download uses `File(bytes, contentType, fileName)`, which emits `Content-Disposition: attachment; filename=…; filename*=UTF-8''…`. Also sets `X-Content-Type-Options: nosniff` and `Cache-Control: private, no-store`. |
| Request size | Upload action: `[RequestSizeLimit(6 MB)]` + `[RequestFormLimits(MultipartBodyLengthLimit = 6 MB)]` (`CustomerAttachmentRules.RequestSizeLimitBytes`). Files between 5 and 6 MB reach the validator and get the friendly "larger than 5 MB" message. Larger requests are rejected by the framework before the handler (see Edge Cases). Angular also checks 5 MB before uploading. |
| Download in Angular | `HttpClient.get(…, { responseType: 'blob' })`. The auth interceptor adds the bearer token because the URL is under `/api/v1` (`auth-interceptor.ts` lines 7–15). Then an object URL, a temporary `<a download>`, and `URL.revokeObjectURL`. The file name comes from the listed metadata. No public or static URL exists. |
| Related tickets | `TicketsApi.list({ customerId, page, pageSize: 10, … })` (`tickets-api.ts` lines 22–45). The server applies `TicketRepository.Scope` (lines 67–69) **before** the `customerId` filter (lines 24–27), so an Agent never sees an unassigned or other-Agent ticket. Collaborator membership never widens it. The UI shows subject, status, priority and created date only. |
| UI placement (C5) | Four new standalone child components rendered as cards inside the existing `@if (customer(); as current)` block of `customer-detail-page.html`, after the "Edit details" card (line 99). Each loads independently, so one failing section never blocks the others or the CUST-001 cards. |

---

## Context — Read These Files First

1. `.squad/stories/customer-management/CUST-002/intake.md` — §2 role table, §3–§5 feature requirements, §6 backend scope ("prefer a small, clear implementation"), §8 test policy, Acceptance criteria and C1–C5 are the contract.
2. [03-story-customer-profiles-contact-management.md](03-story-customer-profiles-contact-management.md) — tone and Verification style for this feature. [../agent-dashboard/07-story-agent-productivity-collaboration.md](../agent-dashboard/07-story-agent-productivity-collaboration.md) — migration review wording (task 7, ~lines 1497–1505) and the `sqlcmd` / PowerShell verification style (~lines 1927–1945).
3. `backend/src/Crm.Domain/Collaboration/TicketInternalMessage.cs` lines 1–19 — **the** template for an append-only entity: private EF ctor, `long Id`, private setters, a single static factory, no mutators. `backend/src/Crm.Domain/Customers/Customer.cs` lines 1–25 (namespace, `Guid Id`).
4. `backend/src/Crm.Infrastructure/Persistence/Configurations/TicketInternalMessageConfiguration.cs` lines 9–22 (identity id, `Restrict` FKs to the parent and to `ApplicationUser`, composite descending index). `CustomerConfiguration.cs` lines 8–28. `TicketConfiguration.cs` lines 20–22 (enum stored as string).
5. `backend/src/Crm.Infrastructure/Persistence/CrmDbContext.cs` lines 19–27 (DbSets) and 34 (`ApplyConfigurationsFromAssembly`, so new configurations need no registration). `backend/src/Crm.Infrastructure/DependencyInjection.cs` lines 37–46 (repository registrations).
6. `backend/src/Crm.Infrastructure/Persistence/Repositories/CollaborationRepository.cs` lines 44–74 — paged list (`CountAsync` + `orderby` before `select` + `Skip/Take`), author join, read-back after insert (`GetMessageAsync`). `CustomerRepository.cs` lines 9–55 (customer queries, `AsNoTracking`).
7. `backend/src/Crm.Application/Collaboration/PostTicketMessage/PostTicketMessageCommand.cs` lines 16–45 — command, handler and validator in **one file**, trim on save, read-back. `ListTicketMessages/ListTicketMessagesQuery.cs` lines 10–29 (paged query, `Math.Min(pageSize, Paging.MaxPageSize)`, page validator). `CollaborationDetails.cs` line 15 (`TicketMessageDetails` read-model shape).
8. `backend/src/Crm.Application/Customers/GetCustomer/GetCustomerQuery.cs` lines 9–13 (404 text). `CreateCustomer/CreateCustomerCommand.cs` lines 12–32 (single save, `TimeProvider`, `Guid.CreateVersion7()`). `Tickets/CreateTicket/CreateTicketCommand.cs` lines 90–92 (`Invalid(property, message)` ⇒ 400 helper).
9. `backend/src/Crm.Application/Common/Validation/CustomerRules.cs` and `ProductivityRules.cs` (rules-constants style). `Common/Paging/PagedResult.cs` lines 3–11. `Abstractions/Persistence/ICollaborationRepository.cs` lines 8–35 (doc-comment style). `Abstractions/Persistence/IUnitOfWork.cs` lines 7–20.
10. `backend/src/Crm.Api/Controllers/CustomersController.cs` lines 15–58 (class attributes, `EndpointDescription` wording, `[FromQuery]` paging). `CollaborationsController.cs` lines 21–61 (nested routes, `PagedResponse` mapping, `CreatedAtAction` to the list action). `Contracts/Collaboration/CollaborationContracts.cs` lines 1–40 (several records + `From(...)` in one file). `Contracts/Common/PagedResponse.cs` line 4. `ApiRoutes.cs` line 10.
11. `backend/src/Crm.Api/Errors/GlobalExceptionHandler.cs` lines 20–35 — `ValidationException` ⇒ 400 with camelCased keys (so `"file"` stays `file`), `NotFoundException` ⇒ 404. **No change needed.**
12. `backend/tests/Crm.UnitTests/Customers/CreateCustomerCommandHandlerTests.cs` — whole file (55 lines): naming, hand-written fakes (36–54). This is the pattern for the single new test.
13. `frontend/src/app/features/customers/customer-detail-page.ts` lines 18–64 (standalone component, signals, `takeUntilDestroyed`) and `customer-detail-page.html` lines 9–101 (the `@if (customer(); as current)` block; the "Edit details" card ends on line 99 and `</div>` closes `detail-stack` on line 100). `customer-detail-page.scss` lines 1–38 (`detail-stack`, `hint`, `form-error`).
14. `frontend/src/app/features/collaboration/ticket-discussion.ts` lines 17–129 and `ticket-discussion.html` lines 1–44 — **the** template for the Notes and Interactions components: `Subject` + `switchMap` paged loader with dedupe, composer `FormControl`, `loading` / `loaded` / `error` / `postError` signals, "Load older" button, plain-text rendering via interpolation only. `ticket-discussion.scss` lines 1–36 (`.composer`, `.thread`).
15. `frontend/src/app/features/customers/customers-api.ts` lines 1–38 (`ApiUrl.versioned`, `HttpParams`) and `customers-api.spec.ts` lines 1–44 (**the** spec pattern for the single new frontend test). `customer-models.ts` lines 14–20 (`PagedResponse<T>`).
16. `frontend/src/app/features/tickets/tickets-api.ts` lines 22–45 (`list` with `customerId`). `ticket-models.ts` lines 14–47 (`Ticket`, `TicketListParams`), line 67 (`priorityTone`), and lines 118–131 (`statusLabel`, `statusTone`). `shared/ui/status-badge/status-badge.ts` line 3 (`StatusTone`).
17. `frontend/src/app/features/tasks/task-models.ts` lines 80–91 — `fromLocalInput` / `toLocalInput` for `datetime-local`. **Reuse them; do not copy them.**
18. `frontend/src/app/core/api/describe-http-error.ts` lines 5–14 (returns `detail ?? title`, so a 400 shows only the generic title) and `problem-details.ts` lines 1–10 (`errors?: Record<string, string[]>`). `core/auth/auth-interceptor.ts` lines 7–15 (bearer token for every `/api/v1` URL, including blob requests).
19. `frontend/src/app/app.routes.ts` lines 35–39 — `/customers/**` is already `roleGuard('SuperAdmin', 'Agent')`. **No route change.**
20. Docs: `docs/customer-management.md` (§3 matrix lines 23–34, §4 API lines 36–49, §7 lines 60–62), `docs/identity-and-access.md` (matrix line 14, routes lines 47–48), `docs/api-conventions.md` lines 3–27, `README.md` line 3.

---

## Product rules (from story)

### Role matrix (server-enforced)

| Operation | SuperAdmin | Agent | Customer |
|---|---|---|---|
| View customer profile (CUST-001) | unchanged | unchanged | own self-profile only (unchanged) |
| Add / list notes | any customer | any customer | **403** |
| Log / list interactions | any customer | any customer | **403** |
| Upload / list / download attachments | any customer | any customer | **403** |
| Edit / delete note or interaction | **no endpoint** (405/404 from routing) | **no endpoint** | **403** |
| Delete attachment | **no endpoint** | **no endpoint** | **403** |
| Related tickets on Customer Details | all of the customer's tickets | only the customer's tickets **assigned to the caller** | — |

### Current vs new behaviour

| Area | Current (CUST-001) | New (CUST-002) |
|---|---|---|
| Customer Details page | Summary + Edit details cards | Same two cards, **unchanged**, then Notes, Interactions, Related Tickets and Attachments cards |
| `GET /api/v1/customers/{id}` | `CustomerResponse` | **Unchanged.** No notes, interactions or attachment data or bytes added |
| Ticket visibility | `TicketRepository.Scope` | **Unchanged.** Related Tickets is a filtered call to the existing list |

---

## Backend Tasks

### 1 — Domain (`backend/src/Crm.Domain/Customers/`)

**Create file: `backend/src/Crm.Domain/Customers/CustomerNote.cs`**

```csharp
namespace Crm.Domain.Customers;

/// <summary>
/// Internal staff note on a CRM customer (CUST-002). Append-only (C2 Option A): no edit/delete methods exist —
/// a correction is a new note. Never visible to customers and unrelated to TicketActivity / ticket discussions.
/// Id is a store-generated identity (insertion order breaks ties). AuthorUserId is an Identity user id set by the server only.
/// </summary>
public sealed class CustomerNote
{
    private CustomerNote() { } // EF Core

    public long Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static CustomerNote Add(Guid customerId, Guid authorUserId, string body, DateTimeOffset now) =>
        new() { CustomerId = customerId, AuthorUserId = authorUserId, Body = body, CreatedAtUtc = now };
}
```

**Create file: `backend/src/Crm.Domain/Customers/CustomerInteractionType.cs`**

```csharp
namespace Crm.Domain.Customers;

/// <summary>Manually logged contact kinds (C3). Stored as the member name. Channel integrations are a later module.</summary>
public enum CustomerInteractionType
{
    PhoneCall = 0,
    Meeting = 1,
    Other = 2,
}
```

**Create file: `backend/src/Crm.Domain/Customers/CustomerInteraction.cs`**

```csharp
namespace Crm.Domain.Customers;

/// <summary>
/// Manually logged customer interaction (CUST-002). Append-only (C2 Option A). OccurredAtUtc = when the contact happened
/// (may be in the past); CreatedAtUtc = when it was recorded. RecordedByUserId is set by the server only.
/// </summary>
public sealed class CustomerInteraction
{
    private CustomerInteraction() { } // EF Core

    public long Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public CustomerInteractionType Type { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static CustomerInteraction Record(
        Guid customerId, CustomerInteractionType type, string summary, DateTimeOffset occurredAtUtc, Guid recordedByUserId, DateTimeOffset now) =>
        new()
        {
            CustomerId = customerId,
            Type = type,
            Summary = summary,
            OccurredAtUtc = occurredAtUtc,
            RecordedByUserId = recordedByUserId,
            CreatedAtUtc = now,
        };
}
```

**Create file: `backend/src/Crm.Domain/Customers/CustomerAttachment.cs`**

```csharp
namespace Crm.Domain.Customers;

/// <summary>
/// Small staff-only customer file stored in SQL Server (C4). FileName is sanitized display metadata, never a path.
/// ContentType comes from the server's extension allow-list, never from the client. No delete/replace in CUST-002.
/// Content is only read by the download query; metadata queries never select it.
/// </summary>
public sealed class CustomerAttachment
{
    private CustomerAttachment() { } // EF Core

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public int SizeBytes { get; private set; }
    public byte[] Content { get; private set; } = [];
    public Guid UploadedByUserId { get; private set; }
    public DateTimeOffset UploadedAtUtc { get; private set; }

    public static CustomerAttachment Upload(
        Guid id, Guid customerId, string fileName, string contentType, byte[] content, Guid uploadedByUserId, DateTimeOffset now) =>
        new()
        {
            Id = id,
            CustomerId = customerId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = content.Length,
            Content = content,
            UploadedByUserId = uploadedByUserId,
            UploadedAtUtc = now,
        };
}
```

### 2 — Application: rules, read models, repository contract

**Create file: `backend/src/Crm.Application/Common/Validation/CustomerRecordRules.cs`**

```csharp
namespace Crm.Application.Common.Validation;

public static class CustomerRecordRules
{
    public const int NoteMaxLength = 2000;
    public const int InteractionSummaryMaxLength = 500;
    public static readonly TimeSpan InteractionFutureTolerance = TimeSpan.FromMinutes(5); // client clock skew
    public static readonly DateTimeOffset InteractionEarliest = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
}

/// <summary>C4 attachment rules. Enforced on the server; the Angular checks are a usability aid only.</summary>
public static class CustomerAttachmentRules
{
    public const int MaxBytes = 5 * 1024 * 1024;
    /// <summary>Whole multipart request cap: above MaxBytes so 5–6 MB files still get the friendly validation message.</summary>
    public const long RequestSizeLimitBytes = 6 * 1024 * 1024;
    public const int FileNameMaxLength = 255;
    public const string AllowedTypesText = "PDF, PNG, JPG/JPEG, TXT or DOCX";

    private static readonly IReadOnlyDictionary<string, string> ContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".txt"] = "text/plain",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    };

    private static readonly byte[] Pdf = "%PDF-"u8.ToArray();
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04]; // DOCX is a zip package

    /// <summary>Display name only: last path segment, no control characters, trimmed. Never used as a path.</summary>
    public static string SanitizeFileName(string? fileName)
    {
        var name = (fileName ?? string.Empty).Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        return new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
    }

    /// <summary>Server content type for an already sanitized, allowed file name.</summary>
    public static string ContentTypeFor(string sanitizedFileName) => ContentTypes[Path.GetExtension(sanitizedFileName)];

    /// <summary>First problem with the upload, or null when it is acceptable.</summary>
    public static string? Check(string? fileName, byte[] content)
    {
        if (content.Length == 0)
        {
            return "Select a file to upload. Empty files are not accepted.";
        }

        if (content.Length > MaxBytes)
        {
            return "The file is larger than 5 MB.";
        }

        var name = SanitizeFileName(fileName);
        if (name.Length == 0)
        {
            return "The file must have a name.";
        }

        if (name.Length > FileNameMaxLength)
        {
            return $"The file name is too long (max {FileNameMaxLength} characters).";
        }

        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (!ContentTypes.ContainsKey(extension))
        {
            return $"This file type is not allowed. Upload a {AllowedTypesText} file.";
        }

        var signatureOk = extension switch
        {
            ".pdf" => content.AsSpan().StartsWith(Pdf),
            ".png" => content.AsSpan().StartsWith(Png),
            ".jpg" or ".jpeg" => content.AsSpan().StartsWith(Jpeg),
            ".docx" => content.AsSpan().StartsWith(Zip),
            _ => !content.AsSpan().Contains((byte)0), // .txt: plain text has no NUL bytes
        };
        return signatureOk ? null : "The file content does not match its extension.";
    }
}
```

**Create file: `backend/src/Crm.Application/Customers/CustomerRecordDetails.cs`**

```csharp
using Crm.Domain.Customers;

namespace Crm.Application.Customers;

/// <summary>Read models for CUST-002. Names are joined at read time. Attachment metadata never carries the bytes.</summary>
public sealed record CustomerNoteDetails(
    long Id, Guid CustomerId, Guid AuthorUserId, string AuthorName, string Body, DateTimeOffset CreatedAtUtc);

public sealed record CustomerInteractionDetails(
    long Id, Guid CustomerId, CustomerInteractionType Type, string Summary, DateTimeOffset OccurredAtUtc,
    Guid RecordedByUserId, string RecordedByName, DateTimeOffset CreatedAtUtc);

public sealed record CustomerAttachmentDetails(
    Guid Id, Guid CustomerId, string FileName, string ContentType, int SizeBytes,
    Guid UploadedByUserId, string UploadedByName, DateTimeOffset UploadedAtUtc);

/// <summary>Download payload only (GET …/attachments/{id}/content).</summary>
public sealed record CustomerAttachmentFile(string FileName, string ContentType, byte[] Content);
```

**Create file: `backend/src/Crm.Application/Abstractions/Persistence/ICustomerRecordRepository.cs`**

```csharp
using Crm.Application.Common.Paging;
using Crm.Application.Customers;
using Crm.Domain.Customers;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>
/// CUST-002 notes, interactions and attachments of a CRM customer (Customers.Id). Staff-only data, global for SuperAdmin and Agents.
/// Add* only stages; the caller saves through IUnitOfWork. List methods never return rows of another customer.
/// </summary>
public interface ICustomerRecordRepository
{
    Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken = default);

    void AddNote(CustomerNote note);
    Task<CustomerNoteDetails> GetNoteAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Newest first (CreatedAtUtc DESC, Id DESC).</summary>
    Task<PagedResult<CustomerNoteDetails>> ListNotesAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default);

    void AddInteraction(CustomerInteraction interaction);
    Task<CustomerInteractionDetails> GetInteractionAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Newest occurrence first (OccurredAtUtc DESC, Id DESC).</summary>
    Task<PagedResult<CustomerInteractionDetails>> ListInteractionsAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default);

    void AddAttachment(CustomerAttachment attachment);

    /// <summary>Metadata only — never selects Content.</summary>
    Task<CustomerAttachmentDetails> GetAttachmentAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Metadata only — never selects Content. Newest first (UploadedAtUtc DESC, Id DESC).</summary>
    Task<PagedResult<CustomerAttachmentDetails>> ListAttachmentsAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Name, type and bytes. Null when missing or when the attachment belongs to another customer.</summary>
    Task<CustomerAttachmentFile?> FindAttachmentFileAsync(Guid customerId, Guid attachmentId, CancellationToken cancellationToken = default);
}
```

### 3 — Application: use cases (`backend/src/Crm.Application/Customers/…`)

Each file holds command/query + handler + validator, following `PostTicketMessageCommand.cs` lines 16–45. All handlers depend only on `ICustomerRecordRepository`, plus `IUnitOfWork` / `TimeProvider` where needed. Every handler starts with the existence check:

```csharp
if (!await records.CustomerExistsAsync(command.CustomerId, cancellationToken))
{
    throw new NotFoundException($"Customer '{command.CustomerId}' was not found.");
}
```

**Create file: `backend/src/Crm.Application/Customers/AddNote/AddCustomerNoteCommand.cs`**

```csharp
/// <summary>Single save — not transactional. Append-only: there is no edit/delete use case (C2 Option A).</summary>
public sealed record AddCustomerNoteCommand(Guid CustomerId, Guid ActorUserId, string Body) : ICommand<CustomerNoteDetails>;

// Handler(ICustomerRecordRepository records, IUnitOfWork unitOfWork, TimeProvider timeProvider):
//   existence check ⇒ CustomerNote.Add(command.CustomerId, command.ActorUserId, command.Body.Trim(), timeProvider.GetUtcNow())
//   ⇒ records.AddNote(note) ⇒ await unitOfWork.SaveChangesAsync(ct) ⇒ return await records.GetNoteAsync(note.Id, ct).

public sealed class AddCustomerNoteValidator : AbstractValidator<AddCustomerNoteCommand>
{
    public AddCustomerNoteValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.Body).NotEmpty().WithMessage("Enter a note.").MaximumLength(CustomerRecordRules.NoteMaxLength);
    }
}
```

**Create file: `backend/src/Crm.Application/Customers/ListNotes/ListCustomerNotesQuery.cs`**

```csharp
public sealed record ListCustomerNotesQuery(Guid CustomerId, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IQuery<PagedResult<CustomerNoteDetails>>;
// Handler: existence check ⇒ records.ListNotesAsync(query.CustomerId, query.Page, Math.Min(query.PageSize, Paging.MaxPageSize), ct).
// Validator: RuleFor(q => q.Page).InclusiveBetween(1, Paging.MaxPage); RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);
//   (same as ListTicketMessagesQuery.cs lines 22–29).
```

**Create file: `backend/src/Crm.Application/Customers/LogInteraction/LogCustomerInteractionCommand.cs`**

```csharp
/// <summary>Single save. OccurredAtUtc null ⇒ now. Type is nullable only so a missing JSON field fails validation.</summary>
public sealed record LogCustomerInteractionCommand(
    Guid CustomerId, Guid ActorUserId, CustomerInteractionType? Type, string Summary, DateTimeOffset? OccurredAtUtc)
    : ICommand<CustomerInteractionDetails>;

// Handler(ICustomerRecordRepository records, IUnitOfWork unitOfWork, TimeProvider timeProvider):
//   existence check;
//   var now = timeProvider.GetUtcNow();
//   var occurredAt = (command.OccurredAtUtc ?? now).ToUniversalTime();
//   if (occurredAt > now + CustomerRecordRules.InteractionFutureTolerance)
//       throw Invalid(nameof(command.OccurredAtUtc), "The interaction time cannot be in the future.");
//   CustomerInteraction.Record(command.CustomerId, command.Type!.Value, command.Summary.Trim(), occurredAt, command.ActorUserId, now)
//   ⇒ records.AddInteraction ⇒ SaveChangesAsync ⇒ return await records.GetInteractionAsync(interaction.Id, ct).
//   Invalid(...) = the private helper from CreateTicketCommand.cs lines 90–92 (copy it into this handler).

public sealed class LogCustomerInteractionValidator : AbstractValidator<LogCustomerInteractionCommand>
{
    public LogCustomerInteractionValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.Type).NotNull().IsInEnum().WithMessage("Choose Phone Call, Meeting or Other.");
        RuleFor(c => c.Summary).NotEmpty().WithMessage("Enter a summary.").MaximumLength(CustomerRecordRules.InteractionSummaryMaxLength);
        RuleFor(c => c.OccurredAtUtc)
            .GreaterThanOrEqualTo(CustomerRecordRules.InteractionEarliest).WithMessage("Enter a valid interaction time.")
            .When(c => c.OccurredAtUtc is not null);
    }
}
```

**Create file: `backend/src/Crm.Application/Customers/ListInteractions/ListCustomerInteractionsQuery.cs`** — same shape as `ListCustomerNotesQuery` (`ListInteractionsAsync`).

**Create file: `backend/src/Crm.Application/Customers/UploadAttachment/UploadCustomerAttachmentCommand.cs`**

```csharp
/// <summary>
/// Single save. FileName is the client's original name (sanitized here); Content is the whole file (≤ 5 MB, checked by the validator).
/// No client content type: the stored type comes from the extension allow-list.
/// </summary>
public sealed record UploadCustomerAttachmentCommand(Guid CustomerId, Guid ActorUserId, string FileName, byte[] Content)
    : ICommand<CustomerAttachmentDetails>;

public sealed class UploadCustomerAttachmentCommandHandler(
    ICustomerRecordRepository records,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<UploadCustomerAttachmentCommand, CustomerAttachmentDetails>
{
    public async ValueTask<CustomerAttachmentDetails> Handle(UploadCustomerAttachmentCommand command, CancellationToken cancellationToken)
    {
        if (!await records.CustomerExistsAsync(command.CustomerId, cancellationToken))
        {
            throw new NotFoundException($"Customer '{command.CustomerId}' was not found.");
        }

        var fileName = CustomerAttachmentRules.SanitizeFileName(command.FileName);
        var attachment = CustomerAttachment.Upload(
            Guid.CreateVersion7(),
            command.CustomerId,
            fileName,
            CustomerAttachmentRules.ContentTypeFor(fileName),
            command.Content,
            command.ActorUserId,
            timeProvider.GetUtcNow());

        records.AddAttachment(attachment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await records.GetAttachmentAsync(attachment.Id, cancellationToken);
    }
}

public sealed class UploadCustomerAttachmentValidator : AbstractValidator<UploadCustomerAttachmentCommand>
{
    public UploadCustomerAttachmentValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        // One key ("file") so Angular shows one message next to the picker.
        RuleFor(c => c).Custom((c, context) =>
        {
            if (CustomerAttachmentRules.Check(c.FileName, c.Content) is { } problem)
            {
                context.AddFailure("file", problem);
            }
        });
    }
}
```

`ValidationBehavior` runs before the handler, so `ContentTypeFor` is only ever called with an allowed extension.

**Create file: `backend/src/Crm.Application/Customers/ListAttachments/ListCustomerAttachmentsQuery.cs`** — same shape as `ListCustomerNotesQuery` (`ListAttachmentsAsync`).

**Create file: `backend/src/Crm.Application/Customers/DownloadAttachment/GetCustomerAttachmentFileQuery.cs`**

```csharp
public sealed record GetCustomerAttachmentFileQuery(Guid CustomerId, Guid AttachmentId) : IQuery<CustomerAttachmentFile>;
// Handler: await records.FindAttachmentFileAsync(query.CustomerId, query.AttachmentId, ct)
//   ?? throw new NotFoundException($"Attachment '{query.AttachmentId}' was not found.");
// (A missing customer and a foreign attachment both give 404 — no separate existence query needed.)
```

**Do not create** any update or delete command for notes, interactions or attachments.

### 4 — Infrastructure: EF configuration, DbSets, repository

**Create file: `backend/src/Crm.Infrastructure/Persistence/Configurations/CustomerNoteConfiguration.cs`** — mirror `TicketInternalMessageConfiguration.cs` lines 9–22:

```csharp
builder.ToTable("CustomerNotes");
builder.HasKey(n => n.Id);
builder.Property(n => n.Id).ValueGeneratedOnAdd(); // bigint IDENTITY
builder.Property(n => n.Body).IsRequired().HasMaxLength(2000);
builder.HasOne<Customer>().WithMany().HasForeignKey(n => n.CustomerId).OnDelete(DeleteBehavior.Restrict);
builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(n => n.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
builder.HasIndex(n => new { n.CustomerId, n.CreatedAtUtc, n.Id }).IsDescending(false, true, true);
```

**Create file: `backend/src/Crm.Infrastructure/Persistence/Configurations/CustomerInteractionConfiguration.cs`**

```csharp
builder.ToTable("CustomerInteractions");
builder.HasKey(i => i.Id);
builder.Property(i => i.Id).ValueGeneratedOnAdd();
builder.Property(i => i.Type).HasConversion<string>().HasMaxLength(20);
builder.Property(i => i.Summary).IsRequired().HasMaxLength(500);
builder.HasOne<Customer>().WithMany().HasForeignKey(i => i.CustomerId).OnDelete(DeleteBehavior.Restrict);
builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(i => i.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
builder.HasIndex(i => new { i.CustomerId, i.OccurredAtUtc, i.Id }).IsDescending(false, true, true);
```

**Create file: `backend/src/Crm.Infrastructure/Persistence/Configurations/CustomerAttachmentConfiguration.cs`**

```csharp
builder.ToTable("CustomerAttachments");
builder.HasKey(a => a.Id);
builder.Property(a => a.Id).ValueGeneratedNever();
builder.Property(a => a.FileName).IsRequired().HasMaxLength(255);
builder.Property(a => a.ContentType).IsRequired().HasMaxLength(100);
builder.Property(a => a.Content).IsRequired(); // varbinary(max); only the download query selects it
builder.HasOne<Customer>().WithMany().HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Restrict);
builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
builder.HasIndex(a => new { a.CustomerId, a.UploadedAtUtc, a.Id }).IsDescending(false, true, true);
```

Use the same `using` lines as `TicketInternalMessageConfiguration.cs` lines 1–5, with `Crm.Domain.Customers` instead of the collaboration/ticket namespaces.

**File: `backend/src/Crm.Infrastructure/Persistence/CrmDbContext.cs`** — after line 27 add:

```csharp
public DbSet<CustomerNote> CustomerNotes => Set<CustomerNote>();
public DbSet<CustomerInteraction> CustomerInteractions => Set<CustomerInteraction>();
public DbSet<CustomerAttachment> CustomerAttachments => Set<CustomerAttachment>();
```

(`Crm.Domain.Customers` is already imported on line 3.)

**Create file: `backend/src/Crm.Infrastructure/Persistence/Repositories/CustomerRecordRepository.cs`** — `internal sealed class CustomerRecordRepository(CrmDbContext dbContext) : ICustomerRecordRepository`. Follow `CollaborationRepository.cs` lines 44–74:

```csharp
public Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken = default) =>
    dbContext.Customers.AnyAsync(c => c.Id == customerId, cancellationToken);

public void AddNote(CustomerNote note) => dbContext.CustomerNotes.Add(note);

public async Task<PagedResult<CustomerNoteDetails>> ListNotesAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default)
{
    var notes = dbContext.CustomerNotes.AsNoTracking().Where(n => n.CustomerId == customerId);
    var totalCount = await notes.CountAsync(cancellationToken);
    var items = await (from n in notes
                       join author in dbContext.Users on n.AuthorUserId equals author.Id
                       orderby n.CreatedAtUtc descending, n.Id descending
                       select new CustomerNoteDetails(n.Id, n.CustomerId, n.AuthorUserId, author.FirstName + " " + author.LastName, n.Body, n.CreatedAtUtc))
        .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
    return new PagedResult<CustomerNoteDetails>(items, page, pageSize, totalCount);
}

public Task<CustomerNoteDetails> GetNoteAsync(long id, CancellationToken cancellationToken = default) =>
    (from n in dbContext.CustomerNotes.AsNoTracking().Where(n => n.Id == id)
     join author in dbContext.Users on n.AuthorUserId equals author.Id
     select new CustomerNoteDetails(n.Id, n.CustomerId, n.AuthorUserId, author.FirstName + " " + author.LastName, n.Body, n.CreatedAtUtc))
    .SingleAsync(cancellationToken);
```

Interactions: the same pattern, `orderby i.OccurredAtUtc descending, i.Id descending`, joining `RecordedByUserId`.

Attachments: the same pattern, but the projection lists **metadata columns only** (`a.Id, a.CustomerId, a.FileName, a.ContentType, a.SizeBytes, a.UploadedByUserId, uploader name, a.UploadedAtUtc`). **Never** `select a` or `a.Content` in `GetAttachmentAsync` / `ListAttachmentsAsync`.

```csharp
public Task<CustomerAttachmentFile?> FindAttachmentFileAsync(Guid customerId, Guid attachmentId, CancellationToken cancellationToken = default) =>
    dbContext.CustomerAttachments.AsNoTracking()
        .Where(a => a.Id == attachmentId && a.CustomerId == customerId)
        .Select(a => new CustomerAttachmentFile(a.FileName, a.ContentType, a.Content))
        .SingleOrDefaultAsync(cancellationToken);
```

The inner joins to `Users` never drop rows: author / recorder / uploader are required FKs.

**File: `backend/src/Crm.Infrastructure/DependencyInjection.cs`** — after line 46 add `services.AddScoped<ICustomerRecordRepository, CustomerRecordRepository>();`.

### 5 — API: contracts and controller

**Create file: `backend/src/Crm.Api/Contracts/Customers/CustomerRecordContracts.cs`** (one file, like `CollaborationContracts.cs`)

```csharp
using Crm.Application.Customers;
using Crm.Domain.Customers;

namespace Crm.Api.Contracts.Customers;

public sealed record AddCustomerNoteRequest(string Body);

public sealed record CustomerNoteResponse(long Id, Guid AuthorUserId, string AuthorName, string Body, DateTimeOffset CreatedAtUtc)
{
    public static CustomerNoteResponse From(CustomerNoteDetails n) => new(n.Id, n.AuthorUserId, n.AuthorName, n.Body, n.CreatedAtUtc);
}

/// <summary>occurredAtUtc omitted/null = now. type: "PhoneCall" | "Meeting" | "Other".</summary>
public sealed record LogCustomerInteractionRequest(CustomerInteractionType? Type, string Summary, DateTimeOffset? OccurredAtUtc);

public sealed record CustomerInteractionResponse(
    long Id, CustomerInteractionType Type, string Summary, DateTimeOffset OccurredAtUtc,
    Guid RecordedByUserId, string RecordedByName, DateTimeOffset CreatedAtUtc)
{
    public static CustomerInteractionResponse From(CustomerInteractionDetails i) =>
        new(i.Id, i.Type, i.Summary, i.OccurredAtUtc, i.RecordedByUserId, i.RecordedByName, i.CreatedAtUtc);
}

/// <summary>Metadata only. Bytes are served exclusively by GET …/attachments/{id}/content.</summary>
public sealed record CustomerAttachmentResponse(
    Guid Id, string FileName, string ContentType, int SizeBytes, Guid UploadedByUserId, string UploadedByName, DateTimeOffset UploadedAtUtc)
{
    public static CustomerAttachmentResponse From(CustomerAttachmentDetails a) =>
        new(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedByUserId, a.UploadedByName, a.UploadedAtUtc);
}
```

**Create file: `backend/src/Crm.Api/Controllers/CustomerRecordsController.cs`**

```csharp
[ApiController]
[Route(ApiRoutes.V1 + "/customers/{customerId:guid}")]
[Authorize(Policy = AuthorizationPolicies.Staff)]
public sealed class CustomerRecordsController(ISender sender) : ControllerBase
{
    private const string Boundary = " Internal staff data — never visible to customers (Customer role: 403). Unknown customer: 404.";

    [EndpointDescription("Requires role: SuperAdmin or Agent. Internal notes, newest first. One-based page (default 1), pageSize (default 20, clamped to 100)." + Boundary)]
    [HttpGet("notes")]
    public async Task<ActionResult<PagedResponse<CustomerNoteResponse>>> Notes(
        Guid customerId, [FromQuery] int page = 1, [FromQuery] int pageSize = Paging.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ListCustomerNotesQuery(customerId, page, pageSize), cancellationToken);
        return Ok(new PagedResponse<CustomerNoteResponse>(
            result.Items.Select(CustomerNoteResponse.From).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    [EndpointDescription("Requires role: SuperAdmin or Agent. Plain text, 1-2000 characters. Append-only: notes are never edited or deleted." + Boundary)]
    [HttpPost("notes")]
    [ProducesResponseType<CustomerNoteResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CustomerNoteResponse>> AddNote(Guid customerId, AddCustomerNoteRequest request, CancellationToken cancellationToken)
    {
        var note = await sender.Send(new AddCustomerNoteCommand(customerId, User.GetUserId(), request.Body), cancellationToken);
        return CreatedAtAction(nameof(Notes), new { customerId }, CustomerNoteResponse.From(note));
    }

    // Interactions(GET "interactions") and LogInteraction(POST "interactions") — same shape.
    //   Descriptions: "…Manually logged interactions (PhoneCall, Meeting, Other), newest occurrence first…" and
    //   "…summary 1-500 characters; occurredAtUtc optional (default now), not in the future. Append-only. Not a message to the customer."

    // Attachments(GET "attachments") — same shape, metadata only.

    [EndpointDescription("Requires role: SuperAdmin or Agent. multipart/form-data with one field 'file'. Max 5 MB; PDF, PNG, JPG/JPEG, TXT or DOCX (extension and content signature are checked). Attachments cannot be deleted." + Boundary)]
    [HttpPost("attachments")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(CustomerAttachmentRules.RequestSizeLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = CustomerAttachmentRules.RequestSizeLimitBytes)]
    [ProducesResponseType<CustomerAttachmentResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CustomerAttachmentResponse>> UploadAttachment(Guid customerId, IFormFile? file, CancellationToken cancellationToken)
    {
        // Missing file ⇒ empty content ⇒ 400 "file" from the validator. The whole request is capped at 6 MB above.
        byte[] content = [];
        if (file is not null)
        {
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, cancellationToken);
            content = buffer.ToArray();
        }

        var attachment = await sender.Send(
            new UploadCustomerAttachmentCommand(customerId, User.GetUserId(), file?.FileName ?? string.Empty, content), cancellationToken);
        return CreatedAtAction(nameof(DownloadAttachment), new { customerId, attachmentId = attachment.Id }, CustomerAttachmentResponse.From(attachment));
    }

    [EndpointDescription("Requires role: SuperAdmin or Agent. Downloads the file as an attachment (Content-Disposition: attachment, nosniff). 404 when the attachment does not belong to this customer." + Boundary)]
    [HttpGet("attachments/{attachmentId:guid}/content")]
    public async Task<IActionResult> DownloadAttachment(Guid customerId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var file = await sender.Send(new GetCustomerAttachmentFileQuery(customerId, attachmentId), cancellationToken);
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        return File(file.Content, file.ContentType, file.FileName); // sets Content-Disposition: attachment; filename / filename*
    }
}
```

`using` lines: `Crm.Api.Authentication` (for `AuthorizationPolicies`, `GetUserId`), `Crm.Api.Contracts.Common`, `Crm.Api.Contracts.Customers`, `Crm.Application.Common.Paging`, `Crm.Application.Common.Validation`, the seven `Crm.Application.Customers.<UseCase>` namespaces, `Mediator`, `Microsoft.AspNetCore.Authorization`, `Microsoft.AspNetCore.Mvc`. The controller stays thin: no business logic beyond copying the uploaded stream.

### 6 — Migration

From `backend/`:

```powershell
dotnet ef migrations add AddCustomerNotesInteractionsAttachments --project src/Crm.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations -- --environment Development
```

Review the generated `…_AddCustomerNotesInteractionsAttachments.cs` before applying. It must contain **only**:

- three `CreateTable` calls: `CustomerNotes` and `CustomerInteractions` with `bigint` `Id` `SqlServer:Identity`, and `CustomerAttachments` with `Content varbinary(max)` not null;
- FKs to `Customers` and `AspNetUsers`, all `ReferentialAction.Restrict`;
- the three composite indexes;
- the model snapshot update.

**No** `AlterColumn` / `AddColumn` / `DropTable` / `RenameTable` on existing tables, and no `Sql(...)`. Apply with `dotnet ef database update --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` (or `.\run-dev.ps1`). Never use `EnsureCreated`, drop or recreate.

### 7 — Backend unit test (the single new one)

**Create file: `backend/tests/Crm.UnitTests/Customers/UploadCustomerAttachmentCommandHandlerTests.cs`** (pattern: `CreateCustomerCommandHandlerTests.cs`)

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using Crm.Application.Customers;
using Crm.Application.Customers.UploadAttachment;
using Crm.Domain.Customers;

namespace Crm.UnitTests.Customers;

public sealed class UploadCustomerAttachmentCommandHandlerTests
{
    [Fact]
    public async Task Handle_StaffUploadsPdfForExistingCustomer_StoresSanitizedMetadataAndBytesAndSavesOnce()
    {
        var customerId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var now = new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var content = "%PDF-1.7 warranty"u8.ToArray();
        var records = new FakeCustomerRecordRepository(customerId);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UploadCustomerAttachmentCommandHandler(records, unitOfWork, new FixedTimeProvider(now));

        var result = await handler.Handle(
            new UploadCustomerAttachmentCommand(customerId, agentId, @"C:\Users\sara\Documents\warranty.PDF", content),
            CancellationToken.None);

        var stored = Assert.Single(records.Attachments);
        Assert.Equal("warranty.PDF", stored.FileName);          // path stripped: display name only
        Assert.Equal("application/pdf", stored.ContentType);    // from the allow-list, not the client
        Assert.Equal(content, stored.Content);
        Assert.Equal(content.Length, stored.SizeBytes);
        Assert.Equal(agentId, stored.UploadedByUserId);
        Assert.Equal(now, stored.UploadedAtUtc);
        Assert.NotEqual(Guid.Empty, stored.Id);
        Assert.Equal(stored.Id, result.Id);
        Assert.Equal("Test Agent", result.UploadedByName);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    // FakeCustomerRecordRepository(Guid existingCustomerId): CustomerExistsAsync ⇒ id == existingCustomerId;
    //   AddAttachment ⇒ Attachments.Add; GetAttachmentAsync ⇒ maps the added entity to CustomerAttachmentDetails with name "Test Agent";
    //   every other member ⇒ throw new NotSupportedException().
    // FakeUnitOfWork: copy CreateCustomerCommandHandlerTests.cs lines 48–54.
    // FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    //   (same as TransitionTicketStatusCommandHandlerTests.cs; nested private class here — do not share across test files).
}
```

---

## Frontend Tasks

All new files go in `frontend/src/app/features/customers/`. Every component is standalone, `ChangeDetectionStrategy.OnPush`, uses signals, and has `readonly customerId = input.required<string>()`. User text is rendered **only** through interpolation, never `[innerHTML]`. All share one stylesheet, `customer-records.scss`.

### 8 — Models and API service

**Create file: `frontend/src/app/features/customers/customer-records-models.ts`**

```ts
import { HttpErrorResponse } from '@angular/common/http';
import { describeHttpError } from '../../core/api/describe-http-error';
import { ProblemDetails } from '../../core/api/problem-details';

export const CUSTOMER_INTERACTION_TYPES = ['PhoneCall', 'Meeting', 'Other'] as const;
export type CustomerInteractionType = (typeof CUSTOMER_INTERACTION_TYPES)[number];

export function interactionTypeLabel(type: CustomerInteractionType): string {
  return type === 'PhoneCall' ? 'Phone Call' : type;
}

/** Matches Crm.Api CustomerNoteResponse. */
export interface CustomerNote {
  id: number;
  authorUserId: string;
  authorName: string;
  body: string;
  createdAtUtc: string;
}

/** Matches Crm.Api CustomerInteractionResponse. */
export interface CustomerInteraction {
  id: number;
  type: CustomerInteractionType;
  summary: string;
  occurredAtUtc: string;
  recordedByUserId: string;
  recordedByName: string;
  createdAtUtc: string;
}

/** Matches Crm.Api CustomerAttachmentResponse (metadata only). */
export interface CustomerAttachment {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedByUserId: string;
  uploadedByName: string;
  uploadedAtUtc: string;
}

export interface LogCustomerInteractionRequest {
  type: CustomerInteractionType;
  summary: string;
  /** null = now (server time). */
  occurredAtUtc: string | null;
}

/** Usability pre-checks only; the API enforces the real rules (size, extension, content signature). */
export const ATTACHMENT_MAX_BYTES = 5 * 1024 * 1024;
export const ATTACHMENT_ACCEPT = '.pdf,.png,.jpg,.jpeg,.txt,.docx';

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

/** Prefers the first server validation message for `field` (400 ValidationProblemDetails), else describeHttpError. */
export function describeFieldError(error: HttpErrorResponse, field: string, fallback: string): string {
  const problem = error.error as ProblemDetails | null;
  return problem?.errors?.[field]?.[0] ?? describeHttpError(error, fallback);
}
```

**Create file: `frontend/src/app/features/customers/customer-records-api.ts`** — `@Injectable({ providedIn: 'root' }) export class CustomerRecordsApi`, same style as `customers-api.ts` lines 1–38:

```ts
notes(customerId: string, page: number, pageSize: number): Observable<PagedResponse<CustomerNote>>   // GET customers/{id}/notes?page&pageSize
addNote(customerId: string, body: string): Observable<CustomerNote>                                 // POST customers/{id}/notes { body }
interactions(customerId: string, page: number, pageSize: number): Observable<PagedResponse<CustomerInteraction>>
logInteraction(customerId: string, body: LogCustomerInteractionRequest): Observable<CustomerInteraction>
attachments(customerId: string, page: number, pageSize: number): Observable<PagedResponse<CustomerAttachment>>

/** multipart/form-data with one 'file' field. Do not set Content-Type: the browser adds the boundary. */
uploadAttachment(customerId: string, file: File): Observable<CustomerAttachment> {
  const form = new FormData();
  form.append('file', file, file.name);
  return this.http.post<CustomerAttachment>(this.apiUrl.versioned(`customers/${customerId}/attachments`), form);
}

/** Authenticated download (the interceptor adds the bearer token); there is no public file URL. */
downloadAttachment(customerId: string, attachmentId: string): Observable<Blob> {
  return this.http.get(this.apiUrl.versioned(`customers/${customerId}/attachments/${attachmentId}/content`), { responseType: 'blob' });
}
```

`PagedResponse` is imported from `./customer-models`.

### 9 — Notes card

**Create files: `customer-notes.ts`, `customer-notes.html`** (selector `crm-customer-notes`). Copy the structure of `ticket-discussion.ts` lines 17–129 / `.html` lines 1–44, with these differences:

- `PAGE_SIZE = 10`. The API calls are `api.notes(...)` / `api.addNote(...)`. There is **no** `accessLost` output: a load error shows `describeHttpError(error, 'Could not load notes.')` plus a "Retry" button.
- Composer: `FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(2000)] })`, textarea label "Add a note", hint "{{ length }} / 2000", button **"Add note"** (disabled while posting or when the trimmed value is empty). On success, prepend the note, increment `totalCount`, reset the composer, and show the snackbar "Note added.". On error, `postError` = `describeFieldError(error, 'body', 'Could not add the note.')` and the typed text is kept.
- Banner (reuse the `.internal-banner` markup from `ticket-discussion.html` line 1): "Internal only — not visible to customers. Notes cannot be edited or deleted; add a new note to correct one."
- Empty state "No notes yet." Each item shows `authorName`, `createdAtUtc | date: 'medium'`, and `body` in a `<p class="user-text">`. "Load older notes" appears when `hasMore()`.
- **No** edit or delete buttons.

### 10 — Interactions card

**Create files: `customer-interactions.ts`, `customer-interactions.html`** (selector `crm-customer-interactions`). Same loader / list / "Load older interactions" pattern as task 9 (`api.interactions`).

- Form (`NonNullableFormBuilder`): `type` (default `'PhoneCall'`, `mat-select` over `CUSTOMER_INTERACTION_TYPES` showing `interactionTypeLabel`), `summary` (`Validators.required`, `Validators.maxLength(500)`, textarea with "{{ length }} / 500" hint), `occurredAt` (`<input matInput type="datetime-local">`, `Validators.required`, initial value `toLocalInput(new Date().toISOString())`). Import `toLocalInput` / `fromLocalInput` from `'../tasks/task-models'`.
- Client pre-check: if `new Date(fromLocalInput(occurredAt)!) > Date.now() + 5 * 60_000`, show "The interaction time cannot be in the future." and do not submit.
- **"Save interaction"** ⇒ `api.logInteraction(customerId, { type, summary: summary.trim(), occurredAtUtc: fromLocalInput(occurredAt) })`. On success, insert the returned item at the position given by `occurredAtUtc` (descending; a back-dated entry lands further down), increment `totalCount`, reset `summary`, reset `occurredAt` to now, and show the snackbar "Interaction saved.". On error, show the server message from the first of `describeFieldError(error, 'occurredAtUtc' | 'summary' | 'type', …)` that is present, falling back to "Could not save the interaction.".
- Each item: a `crm-status-badge` with `interactionTypeLabel(type)` (tone `neutral`), `occurredAtUtc | date: 'medium'`, "Recorded by {{ recordedByName }}", and `summary` in `<p class="user-text">`. Empty state: "No interactions logged yet."
- Hint under the title: "Manual log only — no email, WhatsApp or SMS is sent or received." Notes cannot be edited or deleted. **No** edit or delete buttons.

### 11 — Related Tickets card

**Create files: `customer-related-tickets.ts`, `customer-related-tickets.html`** (selector `crm-customer-related-tickets`).

- Use `TicketsApi.list({ page, pageSize: 10, search: null, customerId: this.customerId(), status: null, category: null, priority: null })`, with the same `Subject` + `switchMap` paged loader and dedupe by `id`, and a "Load more tickets" button.
- Each row: `<a [routerLink]="['/tickets', t.id]">{{ t.subject }}</a>`, `crm-status-badge` with `statusLabel(t.status)` / `statusTone(t.status)`, a priority badge (`t.priority`, `priorityTone(t.priority)`), and `t.createdAtUtc | date: 'mediumDate'`. Import from `../tickets/ticket-models`. Do **not** render the description, customer email, assignee or escalation fields.
- Hint: "Only tickets you are allowed to open are listed." Empty state: "No tickets you can access for this customer." Error: `describeHttpError(error, 'Could not load related tickets.')` + Retry.
- No "create ticket" action. Ticket creation stays on `/tickets/new`.

### 12 — Attachments card

**Create files: `customer-attachments.ts`, `customer-attachments.html`** (selector `crm-customer-attachments`). Same paged list loader (`api.attachments`, "Load more files").

- Picker: a hidden `<input #picker type="file" [accept]="accept" (change)="onPicked(picker)">` plus a `mat-stroked-button` **"Choose file"** that calls `picker.click()`. Show the selected name and `formatFileSize(size)`.
- `onPicked`: take `files?.[0]`. Client errors (shown in `.form-error`, upload button disabled): size 0 ⇒ "Empty files cannot be uploaded."; size > `ATTACHMENT_MAX_BYTES` ⇒ "The file is larger than 5 MB."; extension not in `ATTACHMENT_ACCEPT` ⇒ "Upload a PDF, PNG, JPG/JPEG, TXT or DOCX file.".
- **"Upload"** ⇒ `api.uploadAttachment(customerId, file)`. While uploading, show `uploading` (indeterminate progress bar, buttons disabled). On success, prepend the item, increment `totalCount`, clear the selection and `picker.value = ''`, and show the snackbar "File uploaded.". On error, `describeFieldError(error, 'file', 'Could not upload the file.')`.
- List rows: file name, `formatFileSize(sizeBytes)`, "Uploaded by {{ uploadedByName }}", `uploadedAtUtc | date: 'medium'`, and a **"Download"** button.
- `download(a)`: `api.downloadAttachment(customerId, a.id)` ⇒ `const url = URL.createObjectURL(blob); const link = document.createElement('a'); link.href = url; link.download = a.fileName; link.click(); URL.revokeObjectURL(url);`. On error, show the snackbar "Could not download the file." (a blob error body has no ProblemDetails). Track the in-flight download id in a signal to disable that button.
- Hint: "Staff only. Max 5 MB. PDF, PNG, JPG/JPEG, TXT or DOCX. Files cannot be deleted." **No** delete button. No preview or inline rendering of any file.

**Create file: `frontend/src/app/features/customers/customer-records.scss`** — `@use '../../shared/ui/productivity-page';` plus the `.internal-banner`, `.composer` and `.thread` rules from `ticket-discussion.scss` lines 3–36, plus `.record-meta { display: flex; flex-wrap: wrap; gap: var(--crm-space-2) var(--crm-space-3); align-items: center; }`. All four components use `styleUrl: './customer-records.scss'`.

### 13 — Customer Details integration

**File: `frontend/src/app/features/customers/customer-detail-page.ts`** — add `CustomerNotes`, `CustomerInteractions`, `CustomerRelatedTickets`, `CustomerAttachments` to `imports` (lines 21–31). No other change.

**File: `frontend/src/app/features/customers/customer-detail-page.html`** — insert after line 99 (end of the "Edit details" card), before the `</div>` on line 100:

```html
    <mat-card appearance="outlined">
      <mat-card-header><mat-card-title>Notes</mat-card-title></mat-card-header>
      <mat-card-content><crm-customer-notes [customerId]="current.id" /></mat-card-content>
    </mat-card>

    <mat-card appearance="outlined">
      <mat-card-header><mat-card-title>Interactions</mat-card-title></mat-card-header>
      <mat-card-content><crm-customer-interactions [customerId]="current.id" /></mat-card-content>
    </mat-card>

    <mat-card appearance="outlined">
      <mat-card-header><mat-card-title>Related tickets</mat-card-title></mat-card-header>
      <mat-card-content><crm-customer-related-tickets [customerId]="current.id" /></mat-card-content>
    </mat-card>

    <mat-card appearance="outlined">
      <mat-card-header><mat-card-title>Attachments</mat-card-title></mat-card-header>
      <mat-card-content><crm-customer-attachments [customerId]="current.id" /></mat-card-content>
    </mat-card>
```

Update the page header subtitle on line 1 to `"Profile, contact details, notes, interactions, tickets and files."`. The Summary and Edit details cards, the `save()` logic and `customer-detail-page.scss` stay **unchanged**. The cards are rendered only after the customer loaded, so an unknown id still shows only the existing load error. **No route or navigation change**: `/customers/:id` is already Staff-only (`app.routes.ts` lines 35–39).

### 14 — Frontend unit test (the single new one)

**Create file: `frontend/src/app/features/customers/customer-records-api.spec.ts`** (pattern: `customers-api.spec.ts` lines 1–44)

```ts
describe('CustomerRecordsApi', () => {
  it('uploads one file as multipart form data to the customer attachments endpoint and returns its metadata', () => {
    // TestBed: provideHttpClient(), provideHttpClientTesting(), { provide: API_CONFIG, useValue: { baseUrl: '/api', version: 'v1' } }
    // const file = new File(['%PDF-1.7'], 'warranty.pdf', { type: 'application/pdf' });
    // api.uploadAttachment('c1', file).subscribe(a => (result = a));
    // const request = http.expectOne('/api/v1/customers/c1/attachments');
    // expect(request.request.method).toBe('POST');
    // expect(request.request.body instanceof FormData).toBe(true);
    // expect((request.request.body as FormData).get('file')).toBeInstanceOf(File);
    // expect(((request.request.body as FormData).get('file') as File).name).toBe('warranty.pdf');
    // expect(request.request.headers.has('Content-Type')).toBe(false);
    // request.flush({ id: 'a1', fileName: 'warranty.pdf', contentType: 'application/pdf', sizeBytes: 8,
    //   uploadedByUserId: 'u1', uploadedByName: 'Ahmed Agent', uploadedAtUtc: '2030-01-01T09:00:00Z' });
    // expect(result?.fileName).toBe('warranty.pdf'); expect(result?.sizeBytes).toBe(8); http.verify();
  });
});
```

---

## Documentation Tasks

### 15 — `docs/customer-management.md`

- Title ⇒ `# Customer management (CUST-001, CUST-002)`.
- §3 role matrix (lines 27–34): add rows "Add / read internal notes ✔ ✔ —", "Log / read interactions ✔ ✔ —", "Upload / list / download attachments ✔ ✔ —", "Edit / delete notes, interactions or attachments — — —", "Related tickets on Customer Details: ✔ all / ✔ assigned to them / —".
- §4 API table (lines 38–43): add `GET / POST /api/v1/customers/{id}/notes`, `GET / POST …/interactions`, `GET / POST …/attachments` (POST = multipart `file`), `GET …/attachments/{attachmentId}/content`, all Staff, with the 201 / 400 / 404 results. Add a line: Related tickets = existing `GET /api/v1/tickets?customerId=` (scoped).
- New **§8 Notes and interactions (CUST-002)**: ownership (staff-only, global for SuperAdmin and Agents; author / recorder from the token; names joined, not stored), append-only (C2 Option A: corrections are new entries), validation limits, ordering, and occurred-at vs created-at semantics.
- New **§9 Related tickets**: reuse of the scoped ticket list; Agents see only their assigned tickets; collaborator membership grants nothing here.
- New **§10 Attachments**: SQL Server `varbinary(max)` (C4), 5 MB, the allow-list with signature check, ignored client content type, sanitized display name, `Content-Disposition: attachment` + `nosniff` + `no-store`, bytes never in JSON or metadata queries, no delete / preview / antivirus, and the known trade-off (database growth; move to object storage in a later story if volumes grow).
- New **§11 Not Communication Channels**: interactions are manual logs only. No Email / WhatsApp / SMS / Live Chat is sent, received or synced. That is the final roadmap module.
- §7 Future stories (line 62): replace the "CUST-002 notes, CUST-003 attachments, CUST-004 interaction history" sentence with "Notes, interactions and attachments shipped in CUST-002 (§8–§11)". Keep the deferred list and add "attachment deletion, note/interaction corrections in place".

### 16 — Other docs

- `docs/identity-and-access.md`: after matrix line 14 add "Customer notes / interactions / attachments (read / add; append-only, no delete) | ✔ | ✔ | —". After routes line 48 add the four new route rows (SuperAdmin, Agent).
- `docs/api-conventions.md`: add a bullet after line 26: "**Files:** uploads are `multipart/form-data` with one `file` field and a per-action `RequestSizeLimit`; validation errors use the `file` key. Downloads are `GET …/content` returning the bytes with `Content-Disposition: attachment`, `X-Content-Type-Options: nosniff`, `Cache-Control: private, no-store`. Files are never served from static or public URLs. First used by `/api/v1/customers/{id}/attachments` ([customer-management.md](customer-management.md))."
- `README.md` line 3: add "customer notes, interactions & attachments (`CUST-002`)" to the list of delivered stories.

---

## Edge Cases & Failure Modes

- **Customer-role token on any new endpoint:** `[Authorize(Policy = AuthorizationPolicies.Staff)]` on `CustomerRecordsController` ⇒ **403** ProblemDetails (via `UseStatusCodePages`), before any handler runs. The Customer self-profile endpoints do not expose any of these records. Angular never renders the cards for Customers, because `/customers/**` is `roleGuard('SuperAdmin', 'Agent')` (`app.routes.ts` lines 35–39).
- **Unknown or malformed `customerId`:** a malformed GUID does not match `{customerId:guid}` ⇒ 404 from routing. A well-formed unknown id ⇒ handler `CustomerExistsAsync` ⇒ **404** "Customer '…' was not found." for list and create alike (tasks 3). Nothing is inserted.
- **Attachment of another customer** (`GET /customers/A/attachments/{id-of-B}/content`) ⇒ `FindAttachmentFileAsync` filters on both ids ⇒ **404**. No existence leak beyond what staff can already list.
- **Edit/delete attempts:** no `PUT`/`PATCH`/`DELETE` actions exist. Routing returns **405** for a matching path with a different verb, or 404. Verify that `PUT /notes`, `DELETE /notes/{id}` and `DELETE /attachments/{id}` all fail and nothing changes in SQL.
- **Whitespace-only note or summary:** FluentValidation `NotEmpty()` treats whitespace as empty ⇒ 400 `body` / `summary`. A 2,000-character note with surrounding spaces is validated on the raw value (≤ 2,000) and stored trimmed. A 2,001-character raw value ⇒ 400 (the textarea `maxlength` prevents it in the UI).
- **Unicode / emoji / RTL text and HTML-looking text in notes, summaries or file names** (e.g. `<script>alert(1)</script>`): stored verbatim (`nvarchar`) and rendered via Angular interpolation only, so it shows as text and never executes. The download file name goes through `File(..., fileDownloadName)`, which encodes `filename*` as RFC 5987.
- **Missing interaction `type` or an unknown name** (`"Email"`): a missing field ⇒ `Type` null ⇒ 400 `type`. An unknown name or an integer ⇒ JSON binding fails (`allowIntegerValues: false`) ⇒ `[ApiController]` 400.
- **Interaction time in the future / far past / with offset:** > now + 5 min ⇒ 400 `occurredAtUtc` (handler, `TimeProvider`). Before 2000-01-01 ⇒ 400 (validator). Any offset (e.g. `+02:00`) is normalized with `ToUniversalTime()` before saving. The UI sends ISO UTC from `fromLocalInput`.
- **Back-dated interaction:** listed by `OccurredAtUtc DESC`, so it may appear below newer ones. `CreatedAtUtc` still records the logging time. Angular inserts it at the right position (task 10).
- **Missing `file` field / empty file:** `IFormFile?` null ⇒ empty content ⇒ 400 `file` "Select a file to upload…". A 0-byte file is the same.
- **File 5–6 MB:** passes the request limit ⇒ validator ⇒ 400 `file` "The file is larger than 5 MB.". **File > 6 MB:** the request exceeds `[RequestSizeLimit]` / `MultipartBodyLengthLimit`. ASP.NET Core rejects it while reading the form, before the handler, with **400** (model-binding error) or **413**, depending on whether Kestrel or form reading trips first. Either is acceptable, but it must **not** be 500 and nothing may be inserted. Record the actual status in the verification notes. Angular blocks > 5 MB before sending.
- **Disallowed or disguised file:** `.html`, `.exe`, `.svg`, or no extension ⇒ 400 "This file type is not allowed…". An HTML file renamed `fake.pdf` ⇒ signature mismatch ⇒ 400 "The file content does not match its extension.". A TXT containing NUL bytes ⇒ 400. The client `Content-Type` is ignored, so a `.pdf` sent as `text/html` is stored as `application/pdf` only if its bytes start with `%PDF-`.
- **Upper-case or mixed-case extension** (`WARRANTY.PDF`): the allow-list is case-insensitive (`StringComparer.OrdinalIgnoreCase` + `ToLowerInvariant`). The stored name keeps the original case.
- **Path-like file names** (`..\..\evil.pdf`, `/etc/passwd.txt`, `C:\x\a.pdf`): `SanitizeFileName` keeps only the last segment (`evil.pdf`, `passwd.txt`, `a.pdf`). Nothing touches the file system. A name made only of separators or control characters ⇒ 400 "The file must have a name.".
- **Duplicate file names:** allowed. Each upload gets its own id, and the list shows both with their times.
- **Bytes leaking into JSON or metadata queries:** `CustomerAttachmentResponse` has no content property. `GetAttachmentAsync` / `ListAttachmentsAsync` project explicit metadata columns. Verify in the EF SQL log that list queries do not select `[Content]` (Verification 6).
- **Download through the browser:** the blob request carries the bearer token (interceptor). A 401 triggers the interceptor's single refresh + retry. A 404 shows the snackbar error. Because the response is `attachment` + `nosniff`, a TXT containing HTML is downloaded, not rendered.
- **Large blobs and the retrying execution strategy:** `EnableRetryOnFailure` (`Infrastructure/DependencyInjection.cs` line 33) may replay a failed `SaveChanges`. The insert is a single statement in EF's own transaction, so a replay cannot create a duplicate after a reported failure. No extra handling.
- **Related tickets for an Agent with no assigned tickets for the customer:** an empty list with the empty-state text. An unassigned ticket or one assigned to another Agent never appears (`TicketRepository.Scope` runs before the `customerId` filter, lines 22–27). Clicking a listed ticket that was reassigned since loading ⇒ the existing Ticket Details 404 handling.
- **One section's API failing** (e.g. attachments 500): only that card shows its error + Retry. The other cards and the CUST-001 Summary/Edit cards keep working (independent components, task 13).
- **Concurrent additions** (two Agents adding notes at once): append-only inserts never conflict. "Load older" dedupes by `id` (as `ticket-discussion.ts` lines 71–72), so offsets shifted by new items do not duplicate rows.
- **Half-applied migration:** see Migration / Rollback. The app reports pending migrations at startup (`BootstrapIdentityAsync` warning) and new endpoints fail with 500 until the migration is applied. Existing features are unaffected because no existing table changes.

---

## Test Plan

1. **Unit (backend, new — the only one):** `backend/tests/Crm.UnitTests/Customers/UploadCustomerAttachmentCommandHandlerTests.cs` — `Handle_StaffUploadsPdfForExistingCustomer_StoresSanitizedMetadataAndBytesAndSavesOnce` (task 7). Hand-written fakes as in `CreateCustomerCommandHandlerTests.cs`.
2. **Unit (frontend, new — the only one):** `frontend/src/app/features/customers/customer-records-api.spec.ts` — "uploads one file as multipart form data to the customer attachments endpoint and returns its metadata" (task 14). Pattern: `customers-api.spec.ts`.
3. **Existing tests:** unchanged and passing. Backend 7 ⇒ **8**; frontend 7 ⇒ **8**. No existing test file is edited. `ICustomerRepository` is untouched, so its fakes compile as they are.
4. **No** integration, API, SQL, E2E or Playwright suites, and no new test packages (intake §8).
5. **Manual (API via curl or Postman CLI, SQL via `sqlcmd`, browser):** Verification Steps 4–11.

---

## Migration / Rollback

- **Before applying:** record baselines with `sqlcmd -S "(localdb)\MSSQLLocalDB" -d CrmDev -Q "SELECT COUNT(*) FROM Customers; SELECT COUNT(*) FROM Tickets; SELECT COUNT(*) FROM TicketActivities; SELECT COUNT(*) FROM AgentTasks; SELECT COUNT(*) FROM QuickReplyTemplates; SELECT COUNT(*) FROM TicketInternalMessages; SELECT COUNT(*) FROM AspNetUsers;"`. After applying, the same counts must be identical, and the three new tables exist and are empty.
- **Additive only:** three new tables, their FKs and indexes. No existing table, column, index or row changes.
- **Rollback (local):** `dotnet ef database update AddAgentProductivityAndCollaboration --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` drops only the three new tables (and their CUST-002 data), then `dotnet ef migrations remove …` if the migration is not kept. Existing data is untouched.
- **Half-applied state:** EF runs the migration in one transaction on SQL Server, so a failure leaves no partial tables and no `__EFMigrationsHistory` row. Fix the cause and re-run `database update`. If the tables exist but the history row is missing (manual intervention), drop only `CustomerNotes`, `CustomerInteractions` and `CustomerAttachments`, then re-apply.

---

## Verification Steps

1. **Backend builds and tests:** in `backend/` run `dotnet build` (0 errors), then `dotnet test` (**8** passed: 7 existing + `UploadCustomerAttachmentCommandHandlerTests`).
2. **Migration review and layering:** in `backend/`:
   - `dotnet ef migrations list --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` ⇒ only `…_AddCustomerNotesInteractionsAttachments (Pending)`.
   - `dotnet ef migrations script AddAgentProductivityAndCollaboration AddCustomerNotesInteractionsAttachments --project src/Crm.Infrastructure --startup-project src/Crm.Api --output $env:TEMP/AddCustomerNotesInteractionsAttachments.sql -- --environment Development` ⇒ only `CREATE TABLE` ×3 (`[Content] varbinary(max) NOT NULL`, two `bigint IDENTITY` ids), `CREATE INDEX` ×3 plus FK indexes, FKs **without** `ON DELETE CASCADE`, and one `INSERT INTO [__EFMigrationsHistory]`. No `ALTER TABLE [Customers]`, `UPDATE`, `DELETE` or `DROP`.
   - Record baselines (Migration / Rollback), apply with `dotnet ef database update …`, then re-run the counts ⇒ unchanged.
   - `dotnet ef migrations has-pending-model-changes --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` ⇒ "No changes…".
   - `grep -rn "EntityFrameworkCore\|Microsoft.AspNetCore" src/Crm.Application src/Crm.Domain --include=*.cs` ⇒ nothing.
   - `git diff --stat -- src/Crm.Domain/Customers/Customer.cs src/Crm.Application/Customers/CustomerDetails.cs src/Crm.Application/Abstractions/Persistence/ICustomerRepository.cs src/Crm.Api/Controllers/CustomersController.cs src/Crm.Api/Contracts/Customers/CustomerResponse.cs src/Crm.Api/Controllers/TicketsController.cs src/Crm.Infrastructure/Persistence/Repositories/TicketRepository.cs src/Crm.Infrastructure/Persistence/Migrations/2026101* ../tests` ⇒ only the new test file appears (as untracked); no modifications.
3. **Frontend builds and tests:** in `frontend/` run `npx ng build` (no errors; budget warnings acceptable) and `npm test -- --watch=false` (**8** passed).
4. **Setup — dedicated CUST-002 data (nothing existing is deleted, reset or reassigned):** `.\run-dev.ps1`. As SuperAdmin (`admin@admin.com`) create Agents "Hist Alpha" (`hist.alpha@example.test`) and "Hist Bravo" (`hist.bravo@example.test`) on `/agents/new` (if an email exists, use a new suffix). Register a Customer via `/sign-up` (`hist.customer@example.test`). Create the contact-only customer "Sara History" (`sara.history@example.test`) on `/customers/new`. As SuperAdmin create tickets for Sara: **H1** assigned to Hist Alpha, **H2** assigned to Hist Bravo, **H3** unassigned. Get tokens via `POST /api/v1/auth/login` and refer to them as `$admin`, `$alpha`, `$bravo`, `$customer` (keep them out of logs and PR text). `<sara>` = Sara's customer id.
5. **Notes and interactions (API):**
   - `POST /api/v1/customers/<sara>/notes` with `$alpha`, body `{"body":"  Customer prefers afternoon follow-up calls.  "}` ⇒ **201**, `body` trimmed, `authorName` "Hist Alpha". `GET …/notes` with `$bravo` ⇒ the note is listed (global staff read). With `$admin` ⇒ listed.
   - `{"body":"   "}` ⇒ 400 `body`; a 2,001-character body ⇒ 400; unknown customer id ⇒ **404**; `PUT …/notes` and `DELETE …/notes/1` ⇒ 405/404, and the SQL count is unchanged.
   - `POST …/interactions` with `$alpha` `{"type":"PhoneCall","summary":"Called to ask about refund status"}` ⇒ 201, `occurredAtUtc` ≈ now. Then `{"type":"Meeting","summary":"On-site visit","occurredAtUtc":"<yesterday ISO>"}` ⇒ 201. `GET …/interactions` ⇒ PhoneCall first, then the Meeting (occurred-at order). `occurredAtUtc` one day ahead ⇒ 400 `occurredAtUtc`; `"type":"Email"` ⇒ 400; type omitted ⇒ 400 `type`.
   - Paging: add 12 notes, then `GET …/notes?page=2&pageSize=10` ⇒ 3 items (13 total), `totalCount` 13; `pageSize=500` ⇒ `pageSize` 100 in the response.
6. **Attachments (API + SQL):**
   - `curl -s -X POST -H "Authorization: Bearer $alpha" -F "file=@warranty.pdf" http://localhost:5080/api/v1/customers/<sara>/attachments` (a real small PDF) ⇒ **201**, `Location` ends in `/attachments/<id>/content`, and the JSON has no content field. Repeat with a PNG, a JPG, a TXT and a DOCX ⇒ all 201 with the correct `contentType`.
   - `curl -s -D - -o $env:TEMP\dl.pdf -H "Authorization: Bearer $bravo" http://localhost:5080/api/v1/customers/<sara>/attachments/<id>/content` ⇒ 200 with headers `Content-Type: application/pdf`, `Content-Disposition: attachment; filename=warranty.pdf; filename*=UTF-8''warranty.pdf`, `X-Content-Type-Options: nosniff`, `Cache-Control: private, no-store`. `Get-FileHash` of the original and the downloaded file match.
   - Rejections, each ⇒ 400 with `errors.file` and no new row: an `evil.html`; an HTML file renamed `fake.pdf`; a 0-byte `empty.txt`; a 5.5 MB PDF ("larger than 5 MB"); no `file` field. A 7 MB file ⇒ 400 or 413 (record which), never 500.
   - Path name: `curl … -F "file=@warranty.pdf;filename=..\\..\\evil.pdf"` ⇒ 201 with `fileName` `evil.pdf`.
   - Cross-customer: download Sara's attachment id via another customer's route ⇒ 404.
   - SQL: `sqlcmd -S "(localdb)\MSSQLLocalDB" -d CrmDev -Q "SELECT FileName, ContentType, SizeBytes, DATALENGTH(Content) AS Bytes FROM CustomerAttachments; SELECT COUNT(*) FROM CustomerNotes; SELECT Type, Summary FROM CustomerInteractions;"` ⇒ `SizeBytes = Bytes`, `Type` stored as `PhoneCall` / `Meeting`.
   - EF SQL log (Development console): the `GET …/attachments` query selects no `[Content]` column. `GET /api/v1/customers/<sara>` JSON is unchanged (no notes, attachments or bytes).
7. **Customer-role isolation (API):** with `$customer`, every new endpoint (`GET/POST notes`, `GET/POST interactions`, `GET/POST attachments`, `GET …/content`), for Sara and for the customer's own profile id, ⇒ **403**. `GET /api/v1/me/customer-profile` ⇒ 200, unchanged shape. Anonymous (no token) ⇒ 401.
8. **Related tickets (browser + API):** as Hist Alpha open `/customers/<sara>` ⇒ Related tickets lists **H1 only**. As Hist Bravo ⇒ **H2 only**. As SuperAdmin ⇒ H1, H2, H3. Clicking H1 as Alpha opens `/tickets/<H1>`. `GET /api/v1/tickets?customerId=<sara>` with `$alpha` ⇒ `totalCount` 1. If DASH-002 collaboration is used to invite Alpha on H2, Alpha still does **not** see H2 here.
9. **Browser — staff Customer Details:** as Hist Alpha on `/customers/<sara>`: the Summary and Edit details cards behave as before (edit Sara's company ⇒ "Customer updated."). Notes: add a note ⇒ it appears at the top with "Hist Alpha" and the time; whitespace only ⇒ button disabled; no edit or delete controls. Interactions: log a Phone Call (default now) and a back-dated Meeting ⇒ both listed in occurred-at order; a future time ⇒ inline error. Attachments: choose a PDF ⇒ name + size shown ⇒ Upload ⇒ listed; Download ⇒ the browser saves `warranty.pdf`, which opens correctly; choose a 6 MB file or an `.html` ⇒ client error, Upload disabled; no delete button. Empty states show on a fresh customer. With the API stopped, each card shows its own error and Retry, and the page itself still renders. Check the layout at narrow width (DevTools ~400 px): cards stack, no horizontal scroll. As the Customer account, `/customers/<sara>` redirects away (role guard), and `/profile` is unchanged.
10. **Regression smoke:** sign in / out as each role; customer list + create (CUST-001); registration / self-profile; ticket list, detail, workflow transition and activity (TICK-001/002); My Dashboard (DASH-001); a task, a quick-reply copy and a discussion post (DASH-002) ⇒ all work as before.
11. **OpenAPI:** `curl -s http://localhost:5080/openapi/v1.json` contains `/api/v1/customers/{customerId}/notes`, `/interactions`, `/attachments` (with `multipart/form-data`) and `/attachments/{attachmentId}/content`, each with a "Requires role:" description. `CustomerInteractionType` appears as string enum values.
12. **Record honestly:** in the implementation report, list which of steps 4–11 were actually run (and the observed status for the > 6 MB upload). Mark anything not run as **unverified**. Do not claim a browser check passed without performing it.

---

## Done Criteria

- [x] CUST-001 profile/contact behaviour and Stories 01–07 behaviour are unchanged (Verification 9–10). `Customer`, `CustomerDetails`, `CustomerResponse`, `ICustomerRepository`, `CustomersController`, ticket code and existing migrations are not modified.
- [x] Staff can add and list notes (text, author, UTC time, newest first, paged). **No** edit/delete API or UI exists for notes.
- [x] Staff can log and list interactions (`PhoneCall`/`Meeting`/`Other`, summary, occurred-at defaulting to now, past allowed, future rejected, newest occurrence first). **No** edit/delete API or UI exists for interactions.
- [x] Related tickets on Customer Details come from the existing scoped `GET /api/v1/tickets?customerId=`. Agents see only tickets assigned to them, and each links to Ticket Details.
- [x] Staff can upload (multipart `file`), list and download allowed attachments through authenticated APIs and Angular. The download is `Content-Disposition: attachment` with `nosniff`. There is no public URL and no delete.
- [x] Attachment size (≤ 5 MB, empty rejected), extension allow-list and signature, and note/summary/type/time inputs are validated on the server with 400 field errors. File bytes never appear in JSON or metadata queries.
- [x] Customer-role tokens get 403 on every new endpoint, verified by direct API calls.
- [x] Exactly one additive migration, `AddCustomerNotesInteractionsAttachments` (3 tables, `Restrict` FKs, 3 indexes). Pre/post row counts of existing tables are identical.
- [x] The Customer Details page has working Notes, Interactions, Related tickets and Attachments cards with loading, empty and error/Retry states, and plain-text rendering only.
- [x] Exactly one new backend test (`UploadCustomerAttachmentCommandHandlerTests`) and one new frontend test (`customer-records-api.spec.ts`). `dotnet test` = 8 passed, `npm test` = 8 passed, and both builds succeed.
- [x] Manual API, SQL and browser checks are documented with what was actually run. Unrun items are marked unverified.
- [x] `docs/customer-management.md` (§3, §4, §7, new §8–§11), `docs/identity-and-access.md`, `docs/api-conventions.md` and `README.md` are updated: ownership, routes, attachment handling, interaction semantics, and the separation from Communication Channels.
- [ ] Nothing is committed until the user accepts the Story 08 result.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**

---

## Implementation progress (2026-10-18)

**Status: implemented, awaiting independent review. Nothing committed or pushed.**

| Area | Result |
|---|---|
| Backend build | `dotnet build`: 0 errors, 0 warnings |
| Backend tests | `dotnet test`: 8 passed (7 existing + `UploadCustomerAttachmentCommandHandlerTests`) |
| Frontend build | `npx ng build`: succeeds; initial-bundle budget warning only (623.92 kB vs 500 kB; not compared against a pre-change baseline, but all new code is in the lazy `customer-detail-page` chunk) |
| Frontend tests | `npm test -- --watch=false`: 8 passed (7 existing + `customer-records-api.spec.ts`) |
| Migration | `20261018010157_AddCustomerNotesInteractionsAttachments` generated, reviewed (3 `CreateTable`, 6 `Restrict` FKs, 6 indexes, no changes to existing tables) and applied to `CrmDev`. Pre/post row counts of existing tables identical (Customers 10, Tickets 29, TicketActivities 100, AgentTasks 10, QuickReplyTemplates 3, TicketInternalMessages 17, AspNetUsers 20). `has-pending-model-changes`: none. |
| Layering | `Crm.Application` / `Crm.Domain` have no EF Core / ASP.NET Core references. `Customer`, `CustomerDetails`, `CustomerResponse`, `ICustomerRepository`, `CustomersController`, ticket code and existing migrations are unmodified (only the model snapshot gained the new entities). |

### Verification run (API + SQL, against the running Development API)

- Notes: trimmed save, global staff read (Alpha writes, Bravo/Admin read), whitespace and 2,001-char bodies => 400, unknown customer => 404, `PUT notes` => 405, `DELETE notes/1` => 404, HTML/RTL/emoji stored verbatim, paging (`page=2&pageSize=10` => 4 of 14), `pageSize=500` clamped to 100.
- Interactions: default-now and back-dated entries listed in occurred-at order; future time, `"Email"`, omitted type and pre-2000 date => 400; unknown customer => 404.
- Attachments: PDF, PNG, JPG, TXT, DOCX and upper-case `UPPER.PDF` => 201 with correct content types; path name `..\..\evil.pdf` stored as `evil.pdf`; client `text/html` type ignored; `evil.html`, HTML renamed `fake.pdf`, 0-byte file, TXT with NUL, 5.5 MB PDF, missing `file` field => 400 with `errors.file`; **7 MB upload => 400** ("Failed to read the request form", never 500, nothing inserted); unknown customer => 404.
- Download: 200 with `Content-Type: application/pdf`, `Content-Disposition: attachment; filename=warranty.pdf; filename*=UTF-8''warranty.pdf`, `X-Content-Type-Options: nosniff`, `Cache-Control: private, no-store`; SHA-256 of original and download match; another customer's route => 404; unknown id => 404; `DELETE attachment` => 404. Customer JSON keys unchanged.
- SQL: `SizeBytes = DATALENGTH(Content)` for all rows; interaction `Type` stored as `PhoneCall` / `Meeting`. SQL Server plan cache shows the attachment list query selects metadata columns only (no `[Content]`); only the download query selects `[Content]`.
- Role isolation: Customer token => 403 on GET/POST of notes, interactions, attachments and on download; anonymous => 401; `GET /api/v1/me/customer-profile` => 200.
- Related tickets (`GET /tickets?customerId=`): Hist Alpha sees H1 only, Hist Bravo H2 only, SuperAdmin H1+H2+H3.
- OpenAPI: all four new route groups present, attachments POST is `multipart/form-data`, `CustomerInteractionType` is a string enum, descriptions start with "Requires role:".

### Browser verification (reported by the user, not run by the assistant)

The user reported completing, all passing: open an existing customer profile; Notes; Interactions; Related Tickets; Attachments; error handling; responsive layout; Customer-role access; regression smoke. No details (e.g. the reload after a back-dated interaction) were provided beyond the checklist.

### Not performed by the assistant (superseded above for browser items)

- **Browser verification (steps 9, and the browser parts of 8 and 10):** no browser automation tool was available in this session and the Angular dev server was not started. The UI is verified only by a successful `ng build`, the passing unit tests and code review. Not checked in a browser: the four cards' rendering, the file picker and download, per-card error/Retry states with the API stopped, the 400 px layout, Customer-role redirect from `/customers/:id`, and the reload-first-page behaviour after a back-dated interaction.
- **Regression smoke (step 10)** beyond the existing automated tests and the API-level calls above (sign-in as each role, customer/ticket/dashboard flows in the UI).
- The EF command log could not be switched on through an environment variable, so the "no `[Content]` in list queries" check was done from SQL Server's plan cache instead.
- Verification data left in `CrmDev` (nothing existing was modified): Agents Hist Alpha / Bravo, a Hist Customer account, customers Sara History and Other Person, tickets Hist H1–H3, and 15 notes, 2 interactions and 8 attachments on Sara.

### Deviations from the plan

- Frontend: after saving an interaction the card reloads page 1 from the API (the optional improvement requested for this run) instead of inserting the item locally.
- No budget changes were made for the `ng build` initial-bundle warning.