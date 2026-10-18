# Story 11 — AI Features — Business Feature Intake

> **Revision R1 (2026-10-18) — business decision:** all Gemini AI requests are strictly **user-initiated**. This supersedes every "automatic category recommendation" statement below (§3 item 3, AI4 Option A wording, the per-visit default and §13 check 6). Opening Ticket Details or the AI Assistant, switching tickets or reloading never contacts Gemini; the category recommendation is generated only by **Suggest Category** / **Regenerate**. AI still never applies a category by itself: Apply is a separate explicit action.

## Feature

- **Display name:** AI Features — Gemini-Powered Ticket Assistance, Categorization, Solutions & Chatbot
- **Feature slug:** `ai-features`
- **Work item ID:** `AI-001`
- **Work item type:** `Business Feature Story`
- **Story number:** `11` (verify against the current Squad-Kit index)
- **Tracker:** `none` (training project)
- **Status:** `Business decisions AI1–AI8 approved — ready for Squad-Kit planning`
- **Labels:** `ai, gemini, google-ai, free-tier, ticket-summary, suggested-replies, categorization, knowledge-base, retrieval, chatbot, aspnet-core, angular, squad-kit, training`

## Title

Gemini AI Assistance — Ticket Summaries, Customizable Replies, Category Recommendations, Knowledge-Grounded Solutions and Context-Aware Chatbot

## 1. Business goal and authoritative scope

Integrate **real Google Gemini API responses** into the existing Customer Support CRM so authenticated support staff can understand tickets faster, prepare customer-response drafts, identify the most appropriate category, find useful solutions, and ask follow-up questions in a conversational assistant.

The original two-page *Customer Support CRM — Core Features* document lists exactly five areas under **AI Features**: **Ticket summaries; Suggested replies; Automatic categorization; Suggested solutions; AI chatbot**. All five must function end-to-end at a modest training-project depth. The user explicitly approved **AI1–AI8**, recorded in §3. Do not re-open those decisions without a material conflict in the real repository.

**Scope principle:** AI assists authorized staff. It does **not** secretly change ticket data, route tickets, mark SLA responses, resolve tickets, publish Knowledge Base content, or communicate with customers. The feature uses a real Gemini Free Tier API key supplied by the developer **outside source control**; no fake or hard-coded responses may be substituted for the live integration. When the provider is unavailable, ordinary CRM workflows continue normally.

### Mandatory capabilities (all five)

1. **On-demand ticket summaries:** concise, grounded summary of the authorized ticket and relevant history, with Copy and Regenerate.
2. **Customizable suggested replies:** on-demand, editable customer-response drafts with Professional / Friendly / Empathetic / Concise tones and optional staff instructions; Copy and Regenerate; no sending.
3. **On-demand category recommendation, human application (R1):** Gemini analyzes ticket subject/description and recommends **one of the existing ticket categories** with an explanation; an authorized staff member explicitly accepts or rejects the suggestion before any ticket mutation.
4. **AI suggested solutions:** recommend troubleshooting steps based primarily on **Published** Knowledge Base articles, supplementing them with clearly labeled general Gemini suggestions when appropriate; show accurate KB sources.
5. **Staff AI chatbot:** real multi-turn conversational assistant, available generally and from authorized Ticket Details, with optional ticket context and relevant Published Knowledge Base content. Chat is advisory and has no tools that modify CRM data.

Complete working Angular user flows, authorized backend endpoints, configuration, documentation, lightweight tests, and manual verification for all five are required. Do not deliver only static UI or a mocked model.

## 2. Existing application, dependencies and non-regression

- Extend the **already implemented** monorepo, including Stories **01–10**: Architecture, Authentication/roles, Customers, Tickets and workflow, Agent Dashboard/productivity, Notes/attachments, SLA & Automation, and Knowledge Base. Confirm that Story 10 (`KB-001`) is **committed** before implementation; if not, stop and report instead of layering uncommitted stories. Do not assume a particular commit hash.
- Existing backend: **.NET 10 ASP.NET Core** with `Crm.Domain`, `Crm.Application`, `Crm.Infrastructure`, `Crm.Api`, source-generated Mediator/CQRS, FluentValidation, EF Core SQL Server LocalDB, JWT/Identity and established `/api/v1/` patterns.
- Existing frontend: **Angular 22**, standalone components, Angular Material, feature routes, typed API clients, role guards, and responsive shared UI.
- Reuse established **TicketRepository.Scope** semantics: SuperAdmin can access every staff ticket; Agent can access tickets **currently assigned** to them; collaborator-only membership does not grant normal Ticket Details or AI ticket context. Unauthorized ticket IDs resolve through established scoped 404 behavior. Customer accounts have no staff AI access in Story 11.
- Reuse ticket basic-edit/category permissions and activity history instead of bypassing them. Existing basic editing permits SuperAdmin on any ticket and Agent on their currently assigned tickets; verify exact current command and concurrency behavior before planning AI category Apply.
- Reuse existing **Published Knowledge Base** staff visibility (both Internal and Public), authorized article search, and stable article IDs; no access to other Agents' Draft/ReadyForPublication or Archived article bodies. KB access and ticket access are separate checks.
- Preserve SLA snapshots, response recording, automation, escalations, notification bell, quick replies, ticket links, category/priority rules and all earlier feature behavior. AI categorization must **not silently rerun SLA/assignment**, rewrite due dates, or change existing ticket lifecycle semantics.
- **Communication Channels stay LAST** in the roadmap. Generating text is not sending a message; the AI chatbot in this story is **staff-facing**, not live chat with customers. Customer Portal and public chatbot remain separate future modules.

## 3. Approved business decisions — AI1–AI8

All of the following are approved by the user. **The latest AI8 selection is Option A**, superseding an earlier brief selection of Option B.

| ID | Approved business decision | Binding implementation consequence |
| --- | --- | --- |
| **AI1 — Provider** | **Google Gemini API Free Tier**, not OpenAI. | Use a real Google Gemini model eligible under the user's current Free Tier project. Model identifier configurable on the backend; no second AI provider required. |
| **AI2 — Summaries** | **Option A:** On-demand, relevant ticket history, copy/regenerate, no automatic persistence. | Generate only when staff requests; do not generate upon ordinary ticket load. No SQL summary-history table. |
| **AI3 — Suggested replies** | **Option B:** Professional, Friendly, Empathetic, Concise tone; optional staff instructions; edit/copy/regenerate. **No customer messages sent.** | Draft text only; no communication API or automatic first-response record. |
| **AI4 — Categorization** | **Option A (R1: on demand):** AI generates a recommendation **when the user asks**; Agent/authorized staff **explicitly applies** it. | Never save the AI-proposed category as part of inference. Accept is a separate authorized normal ticket edit; reject/no action leaves ticket unchanged. |
| **AI5 — Suggested solutions** | **Option B:** Prioritize **Published Knowledge Base**; may add clearly labeled **general Gemini knowledge**. | Distinguish KB-supported content from unsourced/general suggestions; identify real article sources, never fabricate citations. |
| **AI6 — Chatbot** | **Option B:** Context-aware staff assistant, authorized ticket context when invoked from Ticket Details, Published KB, **multi-turn**; no ticket mutations/customer messages. | General-chat and ticket-context modes; browser-session-only conversation history; reauthorize ticket each request. |
| **AI7 — Generated-result storage** | **Option A:** On-demand results are **not automatically persisted**; review, edit, copy, regenerate. Chat history stays in the active browser session. | No SQL AI-results/transcripts table. Normal approved ticket changes still persist through existing ticket use cases. AI4 automatic *suggestion* is a deliberate display-only exception to the on-demand pattern. |
| **AI8 — API configuration** | **Option A (latest):** Secure backend API key/model configuration, basic usage limits, timeout and error handling. **No SuperAdmin AI settings screen.** | Key in environment variable or .NET user secrets; shared Gemini client and backend-configured model; minimal quota protection and failure UX. |

**Approval gate passed:** AI1–AI8 are settled. The proposed operational details in later sections should be implemented as simple defaults, not represented as separately user-approved choices.

## 4. Roles, permissions and human-control boundaries

| Operation | SuperAdmin | Agent | Customer / anonymous |
| --- | --- | --- | --- |
| Request AI ticket summary | Any authorized ticket | Own currently assigned ticket | No |
| Request/modify/copy AI reply draft | Any authorized ticket | Own currently assigned ticket | No |
| See AI category recommendation | Any authorized ticket | Own currently assigned ticket | No |
| **Apply** recommended category | Through existing authorized basic ticket edit | Through existing authorized basic ticket edit on assigned ticket | No |
| Request suggested solutions | Any authorized ticket | Own currently assigned ticket | No |
| Use general staff chatbot | Yes | Yes | No |
| Use ticket-context chatbot | Any ticket | Own currently assigned ticket | No |
| See KB-sourced results | Published staff-visible KB only | Published staff-visible KB only | No new access |
| Change Gemini key or model through UI | No UI in this story | No UI | No UI |

- All authorization is **server enforced**. Angular route guards and disabled buttons alone are insufficient. Never accept a client-provided actor ID, Agent ID or role as proof of authorization.
- For every ticket-specific AI call, the backend must independently re-fetch the ticket under the caller's **current** access scope. Do not let an Agent send arbitrary ticket fields to the model in lieu of an authorized server read.
- For chat, the client may send its own plain-text **conversation turns** with bounded size, but may not supply trusted system instructions, privileged DB context, or unchecked ticket content. Backend rebuilds trusted prompts and authorization context per request.
- AI output is **untrusted advisory material**. It cannot invoke ticket mutations, select a new assignee, escalate, publish articles, update SLA, or send messages. The only explicit write in this story is the user's normal authorized **Apply Category** action, using the existing ticket update rules and activity/concurrency safeguards.
- Agent authorization is based on **current assignment**; on reassignment, existing Agent must lose ticket-specific AI access on subsequent calls (and should not keep rendering newly retrieved protected ticket details). Collaborator-only access stays insufficient.

## 5. A — On-demand ticket summaries (AI2)

- On authorized **Ticket Details**, show **Generate Summary**. Only clicking it calls the AI API. Display loading, result, **Copy**, **Regenerate**, and error/retry states.
- Assemble bounded factual context on the **backend** from permitted ticket fields: subject, description, category, priority, status, relevant recent ticket activity/history and safe staff notes/interaction summaries where authorized and necessary. Prefer small relevant extracts; do not send all notes, attachments, contact records, or full audit history.
- Output should summarize: customer-reported problem, known actions/history, current state, and unresolved/next steps **only when supported by the supplied facts**. Clearly state if information is missing rather than inventing a resolution or customer contact.
- If no history exists or it predates tracking, do not fabricate earlier actions. Avoid treating notes, internal discussions or **Mark as Responded** as evidence of actual external customer messaging.
- Summaries are ephemeral UI results; copy/regenerate does not write SQL or add a ticket activity event. New regeneration uses fresh authorized context.

## 6. B — Customizable reply drafts (AI3)

- On authorized Ticket Details, **Suggest Reply** opens a simple area with a required tone choice: **Professional**, **Friendly**, **Empathetic**, **Concise**; plus an optional, length-limited plain-text staff instruction.
- Generate a customer-facing **draft** informed by ticket facts and, where helpful, relevant Published KB. Avoid invented promises, confirmed refunds, deadlines, policies, or messages that were never sent. Tone controls style, not factual accuracy or ticket authorization.
- Result appears in an **editable text field**; staff may modify, copy, regenerate, or discard it. When regenerating, use the chosen tone and current optional instruction. Indicate that an AI draft is **not sent**.
- No email, SMS, WhatsApp, live-chat integration, customer communication history, quick-reply auto-save, or `FirstRespondedAtUtc` update. Copying/editing is not a customer response and does not affect SLA.
- Failures show actionable but non-sensitive UI messages; retry does not create a phantom sent response.

## 7. C — Category recommendation and explicit Apply (AI4)

- Gemini must return **exactly one of the current `TicketCategory` values** (`General`, `Technical`, `Billing`, `Other` at the current baseline), plus a short explanation. The backend should validate/parse the result against the **actual** enum; reject invalid, ambiguous or missing values rather than inventing new categories.
- **(Superseded by R1.)** Originally: a single best-effort recommendation per Ticket Details visit for authorized staff (not on ticket creation or in an unbounded polling loop), with optional **Regenerate**. Throttle or skip automatic inference if the provider is unavailable or quota protection would be exceeded. The ticket should load normally while inference is pending.
- Display **Current Category** and **Suggested Category** side by side, with explanation and **Apply Suggestion** / **Dismiss**. If suggested = current, say so; do not send a redundant update.
- **Apply** must run through the existing server-side, scoped **basic ticket edit** behavior (or the smallest existing-equivalent authorized application operation); the backend checks current assignment, accepts only an allowed enum, and preserves other ticket fields. A successful change uses current activity/history and rowversion/concurrency behavior. Never apply the recommendation by trusting the AI response or changing the database directly.
- If staff edits the ticket, ticket category changes concurrently, or Agent loses assignment before Apply, do **not** overwrite an unrelated change from a stale suggestion. Refresh current category/details and return an understandable conflict or lost-access message. This should not prompt an enterprise locking design.
- **SLA and assignment boundary:** Changing category after ticket creation must not recompute existing SLA deadlines or rerun `TicketCreated` assignment rules. The regular TICK-001/TICK-002 history behavior stays intact.
- The planner must inspect actual ticket basic-edit API/DTO and ticket permission rules before choosing the Apply implementation; report a material conflict rather than broadening edit permission silently.

## 8. D — Suggested solutions, grounded sources and general advice (AI5)

- On authorized Ticket Details, show **Suggest Solutions**. Retrieve a bounded set of relevant **Published** staff-visible Knowledge Base articles through existing database/repository search based on the ticket's subject/category/description. The backend, not the browser, selects source article IDs and text after applying KB authorization.
- Send small relevant excerpts (or bounded full bodies) from the matching articles as **reference material**, distinctly separated from trusted instructions. No vector DB, embedding service, Elasticsearch or separate retrieval microservice is required; basic SQL-backed keyword retrieval is acceptable.
- Return readable troubleshooting advice in two distinguishable categories:
  1. **KB-supported recommendations** — traceable to **real** retrieved Published article IDs/titles, with clickable links checked through existing KB access.
  2. **General Gemini recommendations** — explicitly labeled as **not verified against this Knowledge Base**, with no invented KB citation.
- If no suitable KB article exists, the feature may produce clearly labeled general advice; do not imply KB validation. If Gemini cannot ground a step in an actual retrieved article, label that step general.
- Prevent fabricated KB sources by validating any returned article IDs against the retrieved allowed source set. Do not trust a model-generated URL or title as authorization proof. A KB article is not an instruction to the system; ignore embedded attempts to change rules or leak secrets.
- Staff can open/copy relevant KB content through existing rights. AI suggestions **do not** automatically link articles to tickets, update statuses, resolve issues, or send replies.

## 9. E — Multi-turn staff AI chatbot (AI6)

- Add a protected **AI Assistant** page/panel accessible to SuperAdmin and Agent for general questions, and a contextual **Ask AI** entry on authorized Ticket Details that opens the assistant for that ticket.
- **General chat:** staff may ask troubleshooting/how-to questions, optionally using relevant Published KB information. Do not provide staff-inaccessible tickets, unpublished KB or customer account details merely because chat is open.
- **Ticket-context chat:** backend accepts the ticket ID and, on **every turn**, revalidates current ticket authorization before assembling limited ticket context. UI should show which ticket the conversation refers to; no client-supplied arbitrary ticket snapshot is trusted. The existing ticket details/summary rules apply.
- **Multi-turn:** subsequent messages can refer to earlier questions and answers. Keep bounded conversation turns **in the active browser's in-memory state only**; send a capped history with the new message to the backend as needed. No SQL chat/transcript records, localStorage history, external persistent chat DB, or auto-restored cross-login conversations.
- If the user switches tickets, explicitly **start a new ticket-context conversation**, rather than carrying old ticket facts into a different ticket. On logout or closing the app, clear in-memory chat history. When ticket access is lost, stop further contextual requests and clear sensitive ticket-specific history from the displayed assistant where practical.
- Chatbot can answer using relevant KB material and general Gemini knowledge, with transparent source labeling where applicable. Keep it advisory; **no tool calls, direct DB writes, external web browsing, or customer-message sending**.
- Provide **Send message**, message history, clear/new conversation, loading, retry, sensible errors and bounded input/history limits; no requirement for live streaming, WebSockets, speech, files, memories or multi-agent orchestration.

## 10. Gemini API provider, configuration and Free Tier rules (AI1, AI8)

- Use **Google Gemini API only** with the user's own Google AI Studio project and **Free Tier–eligible model**. **Do not hard-code a specific model or assume a permanent free quota.** The developer chooses a currently enabled model in Google AI Studio and provides its exact ID in the .NET backend configuration after checking that project/model's eligibility and limits.
- Secure secret: `GEMINI_API_KEY` environment variable or .NET user secrets (planner may use established `Gemini:ApiKey` configuration binding). The key must **never** be committed, included in `appsettings.json` with a real value, returned via API, logged, or supplied to Angular. Do not request that it be pasted into any prompt or Squad-Kit file. Use a documented, redacted example instead.
- The **ASP.NET Core backend** performs the call to Google's API; the browser calls only the authenticated CRM endpoint. Create **one** focused Infrastructure-level Gemini integration, behind an Application interface, reused by summaries, replies, categorization, solutions and chat. Prefer built-in `HttpClientFactory` and documented Gemini text-generation capabilities; choose the simplest currently supported stateless request flow. Do not assume a provider-managed stored chat session, because AI7 requires non-persistent app conversations.
- Configurable **model ID, timeout, max output size, max prompt/context size, max chat turns, and a small per-user/request usage cap** appropriate for one local API instance and Free Tier. Exact numeric defaults may be selected and documented by the planner, based on current provider constraints. Bounded manual retry/backoff (if any) only for transient failures; no endless retries, distributed limiters, workers or queues.
- Model-specific quota/RPM/TPM/RPD can change. A **429/quota** response should lead to a clear "AI quota/rate limit reached; try later" outcome (optionally honor `Retry-After` if provided), not a generic 500 or an automatic switch to a paid model. Handle missing/invalid key, unsupported model, invalid JSON/shape, safety refusal, empty response, network failure, provider 5xx and timeout separately enough for useful UX and troubleshooting.
- **Privacy warning for Gemini Free Tier:** Google's current Free Tier pricing/billing documentation indicates that content can be used to improve Google products. For this training project, send **only synthetic/demo tickets, customers and KB content** to Gemini; never route actual confidential customer personal information, secrets, credentials, payment-card details, attachments, or production records to the Free Tier. Minimize and bound all text sent. This is a **usage constraint**, not a claim that arbitrary real customer data is safe to submit.
- Avoid storing full prompts, API keys, customer data or raw model replies in diagnostic logs. Log only useful request metadata (operation, status, duration, provider error category) without sensitive payloads. No requirement for a usage dashboard, persistent token billing log or SuperAdmin model settings UI.
- An absent API key should give a clear AI-unavailable result, without making normal CRM startup or normal tickets/KB unusable. Show an appropriate explanatory state in the AI UI rather than silently returning simulated answers.
- **Provider references for planner verification (current as of October 2026):**
  - [Gemini API key documentation](https://ai.google.dev/gemini-api/docs/api-key)
  - [Gemini API reference / text generation](https://ai.google.dev/api)
  - [Gemini billing and Free Tier](https://ai.google.dev/gemini-api/docs/billing)
  - [Gemini per-model pricing and data use](https://ai.google.dev/gemini-api/docs/pricing)
  - [Gemini rate limits](https://ai.google.dev/gemini-api/docs/rate-limits)
  - [Gemini errors](https://ai.google.dev/gemini-api/docs/api-errors)

## 11. Angular UX and integration points

- **Ticket Details:** provide a coherent, unobtrusive AI Assistance area/card with **Summary**, **Suggested Reply**, **Category Suggestion**, **Suggested Solutions**, and a link/button **Ask AI about this ticket**. Reuse existing tickets API and currently accessible ticket object; do not redesign the page.
- **General AI Assistant:** staff-visible lazy route/navigation consistent with existing Angular shell and role guards. Chat ticket context must not be synthesized from a title passed in the URL without backend record checks.
- **Loading/empty/error states:** clear busy indicators; disable repeated submissions appropriately; allow safe retry/regenerate; copy success/failure feedback; text fields not lost unexpectedly on a provider error. Auto category recommendation must be non-blocking and bounded.
- Generated content clearly labeled **AI-generated — review before use**. Reply is editable; summaries/solutions can be copied. Distinguish sourced KB recommendations from general advice visually and textually.
- Output rendering must be safe: treat Gemini response and quoted KB text as untrusted. Use plain text or the existing safe Markdown renderer (if suitable), **never** bypass Angular sanitization/trust arbitrary model HTML. Links to articles use backend-authorized article IDs, not provider-invented URLs.
- Respect responsive mobile widths and current Material style. Angular never receives the Gemini API key, provider credentials or trusted system prompts.

## 12. Architecture, API, persistence and correctness constraints

- **Application:** separate feature use cases and validations for summary, suggested reply, category suggestion, suggested solutions and chat. Keep task-specific prompt/context composition testable, bounded and role-safe. Prefer the existing Mediator/CQRS conventions and verified repository interfaces.
- **Infrastructure:** single Gemini HTTP integration with secure configuration. Parse free-text vs structured category/solution output defensively. Use constrained JSON/enum output for categorization if supported; always validate after model generation.
- **API:** staff-only, versioned `/api/v1/...` routes with clear request/response contracts; ticket-related endpoints take a ticket ID and reauthorize on every call. Planner chooses route names after inspecting existing controller and authorization conventions. Empty/invalid requests return 400; unauthed 401, Customer 403, out-of-scope ticket 404, stale Apply 409 consistent with existing patterns. Provider problems should be mapped to user-safe service/quota/timeout errors (e.g. 429/503/504 as appropriate) without leaking raw provider data.
- **Ticket category application:** reuse existing basic ticket update + history behavior. If a direct category-only action is necessary to avoid clobbering other fields, the planner must explain why, use existing ticket authorization and rowversion practices, and preserve ticket workflow invariants. Do not invent a model-driven privileged mutation path.
- **Knowledge retrieval:** reuse KB's staff authorized Published filter. Only pass returned approved IDs/text to Gemini; validate source IDs in model output. Store no extra article bodies or AI index for this story.
- **Persistence:** **no mandatory new SQL tables or EF Core migration** for ephemeral AI results or chat. If the planner identifies a genuine minimal persistence requirement, surface it as a conflict rather than silently creating an AI logging/session database. Existing ticket edit activity remains durable as in prior stories.
- **No additional application hosting subsystem:** no AI background worker, queue, streaming gateway, WebSockets, vector database, embeddings pipeline, fine-tuning, cross-provider fallback or agent tool-execution framework.
- **Operational correctness:** handle duplicate click, stale ticket details, cancelled requests, input length, output size, provider quota, provider refusal, concurrent category changes and role changes without leaking content or breaking non-AI operations. Keep engineering proportional to a single-machine training project.

## 13. Required manual acceptance matrix and lightweight test policy

- Add exactly **one new backend happy-path unit test** and **one new Angular frontend happy-path unit test** for AI-001 using current xUnit/Vitest conventions, then run all existing unit tests. Useful examples: backend validates/maps a Gemini category recommendation to a real enum without ticket mutation; frontend renders a suggested reply/tone and applies the expected UI state. The planner selects meaningful coverage; no extra test frameworks.
- **Do not** add automated provider integration, SQL/API integration, E2E/Playwright, contract-testing, fuzzing or performance suites unless explicitly requested. Manually verify the real provider, API endpoints, role boundaries and actual browser flows against synthetic demo data. Do not call a mocked provider test a live Gemini integration verification.
- Build backend/frontend; any pre-existing bundle warning should be noted separately. Verify Story 10's committed baseline, no unrelated code changes, no accidental secrets, and normal ticket/SLA/KB data remain intact. If no migration is required, record "no migration / no model changes" rather than creating one for ceremony.

### Mandatory checks

1. **Gemini setup:** with a developer-provided `GEMINI_API_KEY` and a confirmed Free Tier–eligible configured model, a real authorized call succeeds; the key is absent from frontend bundles, Git diffs, API payloads and logs.
2. **Unavailable provider:** missing/invalid API key produces a helpful AI-only error; normal login, ticket management, SLA, KB and other routes still work. No dummy AI result is shown.
3. **Free quota/rate:** intentionally exercise or simulate provider 429 in the adapter during manual verification; useful quota message, bounded retry, no background request storm, and non-AI operations remain available.
4. **Summary:** Agent on assigned ticket requests a summary; content reflects actual subject/description and recent history, can be copied/regenerated, and is not persisted. No fabricated interactions appear when history is empty.
5. **Suggested reply:** select each of the four tones; provide an optional instruction; generate a real draft, edit/copy/regenerate it. Confirm **no outbound communication**, no first-response mark, and no ticket activity/write from draft generation.
6. **Category recommendation:** (R1) the recommendation is generated only when the user clicks Suggest Category; UI shows the current and suggested enums plus explanation. Reject/dismiss makes **no** ticket change. Regenerate works within quota.
7. **Category Apply:** explicit authorized Apply updates only the intended category via existing ticket API/history and observes current ownership/concurrency. If suggestion matches existing category, no redundant write. Ticket SLA due instants and initial-assignment rules are unaffected.
8. **AI suggested solutions:** use a ticket matching a Published KB article; show a source-backed recommendation linked to an **actual accessible** article. Separately show clearly labeled general Gemini advice. With no relevant KB article, don't invent a KB source.
9. **KB restrictions:** unpublished/archived articles are not fed into suggestions/chat for Agents; only Published staff-readable articles are used. Faked model source IDs are rejected/ignored, and malicious KB text does not gain instruction authority.
10. **General chatbot:** Agent asks a question and then a follow-up referring to the previous turn. The second response uses conversation context. Clear/new chat removes earlier context without persisting it in SQL or storage.
11. **Ticket chatbot:** opening chat from Ticket Details gives only authorized current ticket context; subsequent turns recheck access. Switching tickets resets ticket-specific conversation. Assistant cannot mutate ticket data or send any message.
12. **Roles and access:** SuperAdmin can use any staff-ticket AI; Agent can use only currently assigned-ticket AI; Agent after reassignment and collaborator-only Agent get 404; Customer 403 and anonymous 401. Direct API calls cannot bypass this via fake IDs or client-provided ticket text.
13. **Free Tier privacy:** use synthetic tickets/KB/test accounts only. Inspect representative outbound payload shapes to confirm no key, password, unnecessary customer identity fields, raw attachments or excessive ticket history is sent.
14. **Browser UX:** summary/reply/category/solutions/chat UI loads, regenerates, handles timeout/quota/errors, copies text and functions at narrow width; safe rendering of malicious-looking model content does not execute HTML/JS.
15. **Regressions:** Ticket create/edit/workflow/history, assignments, SLA policies/automation/warnings, notifications, Quick Replies, KB management/publishing/search/links, and existing authentication still work. AI inference alone creates no DB changes or ticket history events.
16. **Model settings:** switching the configured eligible Gemini model (or an invalid model) is handled via backend configuration without Angular changes or a SuperAdmin settings page; report which model was actually verified and actual project quota where visible.

- Report actual `dotnet build`, `dotnet test`, `ng build`, `npm test`, provider responses, manual API/SQL/browser outcomes, Free Tier limits encountered, and all unverified steps accurately. Live provider/API quotas may make some tests temporarily impossible; mark them **Unverified**, not passed.
- **Do not commit or push** application changes until independent implementation review and explicit user acceptance. Do not upgrade to a paid provider/model without user approval.

## 14. Explicitly out of scope

- **OpenAI API** or multi-provider switching/fallback; using a ChatGPT subscription as an API credential.
- SuperAdmin AI model/key/feature-toggle/usage settings UI (**AI8 Option B was superseded by Option A**); a persistent AI analytics/billing dashboard.
- Customer Portal assistant, public chatbot, live-chat operator, customer-readable messages, email, WhatsApp, SMS, web forms or automatic outreach. Communication Channels stays last.
- AI automatically changing category/priority/status/assignment/SLA; sending AI replies; automatically accepting KB recommendations; publishing KB articles with AI.
- Saved AI summary history, chat transcripts in SQL, cross-session chat memory, ticket-response delivery records, persistent prompt caches, embeddings/vector stores, full RAG ingestion pipelines or fine-tuning.
- AI agent tools to execute CRM commands; provider-side search/browser tools, web scraping, real-time voice/video, streaming-first chat infrastructure, files/attachments sent to provider, PDF parsing inside AI flows.
- Speculative multi-instance rate limiting, distributed job queues, audit event stores for every inference, enterprise PII redaction platforms, new automated E2E/integration/security/performance suites.

## 15. Dependencies, planning handoff and delivery gates

- **Story:** `AI-001` (Story 11), first story in `ai-features`.
- **Depends on committed:** `ARCH-001`, `AUTH-001`, `TICK-001`, `TICK-002`, `DASH-001/DASH-002`, `CUST-002`, `SLA-001`, and especially **`KB-001`** for Published knowledge retrieval. Verify actual Git status and exact application symbols before planning/implementation.
- **Environment prerequisite for live integration verification:** the developer creates a Gemini API key in **Google AI Studio** and selects a Free Tier–supported model visible in their project. **Do not paste or save the key in this intake, a Git file, a chat, or a code-generation prompt.** Application implementation/planning may proceed using redacted config placeholders; actual provider smoke checks require the local secret.
- **Suggested Squad-Kit plan:** `.squad/plans/ai-features/11-story-ai-features.md` (planner chooses actual name and updates `.squad/plans/ai-features/00-overview.md` and `.squad/plans/00-index.md`). Story intake source path: `.squad/stories/ai-features/AI-001/intake.md`.
- **Suggested planning order:** inspect code/committed baseline → confirm Gemini Free Tier API and local secret configuration → shared minimal provider/context abstractions → summary/reply/category recommendation/solution/chat handlers → scoped API → Angular AI Ticket Details and chatbot → test/docs → real provider/API/SQL/browser manual acceptance.
- **Planning gate:** this intake defines business requirements only. Generate a detailed implementation plan via Squad-Kit and obtain independent review **before** writing application code or migrations. Planning should not commit/push. Surface only genuine repository conflicts; do not reopen AI1–AI8 or silently omit any of the five functions.
- **Implementation gate:** build, lightweight unit testing, manual live Gemini and browser/API checks, independent review, user acceptance, then commit. No automatic commit.

## Original requirement and source note

- The original *Customer Support CRM — Core Features* document, page 2, lists under **AI Features**: *Ticket summaries; Suggested replies; Automatic categorization; Suggested solutions; AI chatbot*. It does not specify provider, prompt design, UI flow, approval behavior, storage, privacy or quotas.
- The details here are driven by the user's **explicit approvals AI1–AI8** plus bounded operational defaults. They must not be misrepresented as additional original-PDF requirements.
- The provider URLs in §10 are current external implementation references, not secret credentials or hard requirements for a specific model version.

## Notes for Squad-Kit planner

This is an **approved business intake**, not a class-by-class solution. Inspect real Ticket, Knowledge Base, activity, API authorization, `TimeProvider`, tests, and Angular conventions before making a detailed plan. Preserve **all five AI features** with genuine Gemini output, maintain human control over ticket changes, handle Gemini Free Tier availability/privacy responsibly, and keep solutions pragmatic for a single-host training CRM. Avoid extra middleware, SDKs, packages, tables and abstractions unless necessary. Maintain **one new backend and one new frontend happy-path unit test**, with manual provider/API/SQL/browser acceptance. **Plan only** at this stage; do not write application code, create migrations, commit or push.
