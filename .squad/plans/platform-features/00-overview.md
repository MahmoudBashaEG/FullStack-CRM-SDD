# platform-features — plan overview

Entry point for the **platform-features** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 15 | [15-story-platform-features-ui-modernization.md](15-story-platform-features-ui-modernization.md) | Platform Features — Ng-Matero-Inspired Full CRM Frontend Redesign, English/Arabic RTL, Functional Calendars & Charts, and Organization Branding | PLAT-001 (tracker: none) | Story 14 ([../security-administration/14-story-security-administration.md](../security-administration/14-story-security-administration.md), committed baseline; Organization Settings `DisplayName`, ETag and audit), Story 13 ([../reports-management/13-story-reports-management.md](../reports-management/13-story-reports-management.md), report datasets and UTC-day semantics), Stories 01–12 (every screen is restyled and must not regress) |

## Dependency notes

- **Story 15** (`PLAT-001`) implements approved decisions UI1, PL3, PL4 and PL5 (all Option A).
  - **UI:** an Ng-Matero-*inspired* shell and design system, built as our own code on Angular Material 22.
    - No Ng-Matero package and no copied code.
    - `chart.js@4.5.1` (MIT) is the only new npm dependency. It is loaded lazily through one wrapper.
  - **Navigation:** the same routes, guards and nav roles, regrouped.
    - Ticket, Customer, Agent Dashboard and SLA pages use `?tab=` deep-linkable tabs.
    - Reports, Admin and KB use tab nav bars over their existing child routes.
  - **Dates:** all 8 native date/time inputs become Material date, range and time pickers.
    - The API formats and saved instants are unchanged (Cairo-safe `shared/date/date-only.ts`).
  - **Localization:** in-house signal `I18n` service, `t` pipe and `crmDate` pipe.
    - English is bundled; Arabic is a typed lazy chunk, so a missing key is a compile error.
    - RTL is applied through the root CDK `Directionality`.
    - The language and light/dark theme are stored per browser (`localStorage`).
  - **Branding:**
    - ADMIN-001 `DisplayName` is the single name source.
    - New anonymous `GET /api/v1/branding` (3 public fields) and `GET /api/v1/branding/logo` (bytes only).
    - The SuperAdmin logo upload/remove and the accent preset reuse the settings ETag and the existing `OrganizationSettingsUpdated` audit action.
  - **Schema:** one additive migration, `AddOrganizationBranding` (4 nullable columns + the `OrganizationLogos` singleton table).
- Shared contracts it touches:
  - `UpdateOrganizationSettingsCommand` / `Request` gain `AccentColor`; its only consumer, the Settings page, is updated.
  - `OrganizationSettingsResponse` gains 5 additive fields.
  - `IOrganizationSettingsRepository` gains 5 members.
  - `describeHttpError` / `describeSaveError` and the label helpers gain an optional translator parameter, defaulting to today's English, so existing specs are unchanged.
  - `report-bars.ts` is removed.
- Unchanged:
  - every backend controller, policy and handler outside organization settings and branding;
  - ticket, SLA, report and CSAT calculations;
  - roles and record-level authorization;
  - AI opt-in behaviour;
  - every existing test.
- Later stories:
  - Organization Structure (multiple departments and branches; explicitly deferred);
  - account-level, cross-device UI preferences, if ever wanted;
  - **Communication Channels stays last.**
