# AI features (AI-001) — Google Gemini assistance

Five advisory AI capabilities for staff (SuperAdmin, Agent): ticket **summaries**, **reply drafts**, **category suggestions**, **KB-grounded solutions** and a **staff chatbot**, all backed by the Google Gemini API (Free Tier). Nothing is saved, nothing is sent to customers, and the only write anywhere is the explicit, human-approved category Apply.

## 1. Decisions

Approved business decisions AI1–AI8 (one provider, on-demand use, no persistence, human approval for category changes, Free Tier limits, no settings screen) are not reopened. Planner decisions:

| # | Decision |
|---|---|
| D1 | Apply uses a category-only edit `PUT /api/v1/tickets/{id}/category` `{ expectedCategory, category }`. The full `PUT /tickets/{id}` is last-write-wins and would resend stale subject/description/priority. Same scope, permissions and `FieldChanged(Category)` activity; 409 when the category changed. |
| D2 | Stateless `generateContent` (not the Interactions API, which stores state by default). Every request body sets `"store": false`. |
| D3 | One new package: `Microsoft.Extensions.Http` (`IHttpClientFactory`). No Google SDK. |
| D4 | Per-user rate limit policy `ai` (default 6 requests/minute). Provider quota is a different 429 (`aiError: QuotaExceeded`). |
| D5 | AI output is rendered as plain text only (no Markdown renderer, no links except server-validated KB article ids). |
| D6 | Sent: subject, description, category, priority, status, escalation flag/reason, created time, the internal first-response flag and the 10 newest activity events **without** actor/agent names. **Not sent:** customer name/e-mail, customer notes/interactions, attachments, tokens. |
| D7 | Retrieval reads **Published** KB articles only, for every role (never drafts, submitted or archived). |
| D8 | Routes: `POST /api/v1/tickets/{ticketId}/ai/{summary, reply-draft, category-suggestion, solutions}` and `POST /api/v1/ai/chat` (action endpoints). |
| D9 (R1) | **All AI is user-initiated.** Opening Ticket Details or the AI Assistant, switching tickets and reloading never contact Gemini. The category suggestion starts only from **Suggest Category**, **Regenerate** or **Retry**; summary, reply draft and solutions from their Generate/Suggest buttons; chat from Send. Apply calls only `PUT /tickets/{id}/category`. Supersedes the earlier "once per visit" rule. |
| D10 | `Gemini:BaseUrl` is configurable (http only for localhost) so a local stub can simulate provider errors. |

## 2. Roles and human control

| Operation | SuperAdmin | Agent | Customer / anonymous |
|---|---|---|---|
| Ticket AI routes (summary, reply draft, category suggestion, solutions) | any ticket | only tickets **currently assigned** (others 404, including collaborator-only) | 403 / 401 |
| `PUT /tickets/{id}/category` (Apply) | any ticket | own assigned | 403 / 401 |
| `POST /ai/chat` without `ticketId` | ✔ | ✔ | 403 / 401 |
| `POST /ai/chat` with `ticketId` | any ticket | own assigned, re-checked **every turn** | 403 / 401 |
| Change key/model | backend configuration only | — | — |

AI is advisory. Reply drafts are labelled "Draft only — not sent to the customer", never set `FirstRespondedAtUtc` and send nothing. The AI use cases have no `IUnitOfWork`, so they cannot write.

## 3. Configuration

Sections `Gemini` and `Ai` in `appsettings.json` (no secrets; `ApiKey` and `Model` stay empty in Git).

| Setting | Default | Range / note |
|---|---|---|
| `Gemini:ApiKey` | — | **Secret.** User secrets `Gemini:ApiKey` or env `GEMINI_API_KEY`. |
| `Gemini:Model` | `gemini-3.1-flash-lite` (a light, token-cheap model; confirm it is Free Tier in AI Studio) | Exact model id that your project shows as Free Tier–eligible in Google AI Studio. No default in source. |
| `Gemini:BaseUrl` | `https://generativelanguage.googleapis.com/v1beta/` | https (http only for localhost), ends with `/`. |
| `Gemini:Timeout` | 30 s | 5 s – 2 min |
| `Gemini:MaxOutputTokens` | 2048 | 256 – 8192 |
| `Gemini:Temperature` | 0.3 | 0 – 1 |
| `Gemini:RetryDelay` | 2 s | 0 – 10 s |
| `Ai:RequestsPerUserPerMinute` | 6 | 1 – 60 |
| `Ai:MaxChatMessages` / `MaxChatHistoryCharacters` | 20 / 16000 | oldest trimmed first |
| `Ai:MaxTicketContextCharacters` / `ActivityEntries` | 6000 / 10 | |
| `Ai:MaxKnowledgeSources` / `KnowledgeExcerptCharacters` | 3 / 1500 | |

```powershell
cd backend
dotnet user-secrets set "Gemini:ApiKey" "<your-gemini-api-key>" --project src/Crm.Api
dotnet user-secrets set "Gemini:Model" "<model-id-from-ai-studio>" --project src/Crm.Api
```

A missing key/model never blocks startup: AI endpoints answer 503 `NotConfigured` and every other feature works. Check your project's Free Tier models and limits in Google AI Studio; quotas change and the daily limit resets at midnight Pacific. Switching models is a configuration change only (no Angular change).

## 4. Free Tier privacy warning

Use **synthetic demo data only**: no real customer data, secrets, card data or attachments. Two separate things apply:

- **(a) API request storage.** The app uses the stateless `generateContent` call and sends `"store": false` on every request, so it keeps no provider-side conversation and relies on none.
- **(b) Google's Free Tier data-use terms.** These apply independently of `store` and may allow Google to use submitted content to improve its products. `store: false` does **not** change them.

What is sent is listed in D6. The key travels in the `x-goog-api-key` header only, never in the URL, logs or any response.

## 5. Feature behaviour

- **Summary:** on demand; context rebuilt on the server from the scoped ticket and its 10 newest events; Copy / Regenerate; nothing stored.
- **Reply draft:** tone `Professional`, `Friendly`, `Empathetic` or `Concise` (required) and an optional instruction (max 500). Up to 3 relevant Published KB excerpts are included. The instruction steers style and content but cannot override the rules or add unsupported facts. Editable; Copy / Regenerate / Discard.
- **Category suggestion (on demand):** the card starts empty with a **Suggest Category** button. Gemini output is constrained to the real `TicketCategory` names and validated server-side. The card shows current vs suggested plus an explanation. **Apply suggestion** calls `PUT /tickets/{id}/category` (409 on a stale page, no SLA/assignment change); **Dismiss** sends nothing.
- **Solutions:** keyword (SQL `LIKE`) retrieval of at most 3 Published articles. The model returns KB-supported steps that cite article ids plus separately labelled general suggestions. The server keeps a KB recommendation only if its id was retrieved; the title comes from the database; unknown ids are demoted to "general".
- **Chat:** `/ai-assistant` (general) or "Ask AI about this ticket" (ticket context). The client sends the capped plain-text history; the server trims again, re-checks ticket access on every turn and validates source ids. History lives only in a root Angular service in memory: cleared by New conversation, a ticket switch, access loss, logout or user change, and lost on reload.

## 6. Prompt trust model and rendering

The system instruction is built only on the server. Ticket fields, activity, KB excerpts and the staff instruction are wrapped in tagged data blocks (`<ticket>`, `<activity>`, `<kb_article id="…">`, `<staff_instruction>`); `<` and `>` inside data are escaped so data cannot close a block, and the rules state that block content is data. Client chat turns are sent as `user`/`model` contents, never as the system instruction. **Grounding:** ticket-specific facts come only from authorized data; general troubleshooting knowledge is allowed but labelled; communications, refunds, actions taken, policies and deadlines are never invented. Output is rendered as text (`white-space: pre-wrap`, interpolation); no `[innerHTML]`.

## 7. API

| Method | Route | Roles | Status codes |
|---|---|---|---|
| POST | `/api/v1/tickets/{id}/ai/summary` | SuperAdmin, Agent (assigned) | 200; 401; 403; 404; 422; 429; 502; 503; 504 |
| POST | `/api/v1/tickets/{id}/ai/reply-draft` | same | + 400 (tone missing/invalid, instruction > 500) |
| POST | `/api/v1/tickets/{id}/ai/category-suggestion` | same | as summary |
| POST | `/api/v1/tickets/{id}/ai/solutions` | same | as summary |
| POST | `/api/v1/ai/chat` | SuperAdmin, Agent | + 400 (0 or > 50 messages, empty/oversized text, last message not User); 404 for a lost ticket |
| PUT | `/api/v1/tickets/{id}/category` | SuperAdmin, Agent (assigned) | 200; 400; 404; 409 |

Every AI error is `application/problem+json` with an `aiError` extension.

| `aiError` | Status | What to check |
|---|---|---|
| `UsageLimit` | 429 | The per-user cap (`Ai:RequestsPerUserPerMinute`); wait for `Retry-After`. |
| `QuotaExceeded` | 429 | Provider RPM/TPM/RPD quota in AI Studio; `Retry-After` when Google supplies it. Never retried. |
| `NotConfigured` | 503 | Set `Gemini:ApiKey` and `Gemini:Model`. |
| `InvalidCredentials` | 503 | The key was rejected. |
| `ModelUnavailable` | 503 | Wrong/retired model id, or Free Tier unavailable. |
| `ProviderUnavailable` | 503 | Provider 5xx/network failure after one bounded retry. |
| `Timeout` | 504 | No answer within `Gemini:Timeout`. |
| `InvalidResponse` | 502 | Empty, malformed or truncated output (raise `Gemini:MaxOutputTokens` if reasoning consumes tokens). |
| `Blocked` | 422 | The provider declined (safety/recitation/etc.). |

Log lines carry metadata only: `Gemini {Operation} succeeded in {ElapsedMs} ms (finish {FinishReason})` and `Gemini {Operation} failed: {FailureKind} (HTTP …, provider status …) after … ms`. Prompts, answers, provider error text, keys and headers are never logged.

## 8. Persistence

None. No table, no migration, no stored prompts, results or transcripts.

## 9. Limitations

Keyword (`LIKE`) retrieval only (English stop-words); the rate limit is in-memory per API instance; a model with "thinking" may spend output tokens on reasoning; `responseSchema` is deprecated in favour of `responseJsonSchema` but still supported (one-field follow-up); a browser reload clears the chat.
