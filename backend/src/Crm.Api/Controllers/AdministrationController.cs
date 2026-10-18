using Crm.Api.Authentication;
using Crm.Api.Contracts.Administration;
using Crm.Api.Contracts.Common;
using Crm.Api.Http;
using Crm.Application.Administration;
using Crm.Application.Administration.GetAccount;
using Crm.Application.Administration.GetOrganizationSettings;
using Crm.Application.Administration.ListAccounts;
using Crm.Application.Administration.ListAuditLogs;
using Crm.Application.Administration.SetAccountStatus;
using Crm.Application.Administration.UpdateOrganizationSettings;
using Crm.Application.Common.Paging;
using Crm.Domain.Administration;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/admin")]
[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
public sealed class AdministrationController(ISender sender) : ControllerBase
{
    private const string Concurrency = " Requires If-Match: the settings ETag (428 if missing, 400 if malformed, 409 if stale).";

    [EndpointDescription("Requires role: SuperAdmin. All accounts (SuperAdmin, Agent, Customer). search on name/email; role (SuperAdmin|Agent|Customer); isActive; one-based page (default 1), pageSize (default 20, clamped to 100). Order: last name, first name, email, id.")]
    [HttpGet("users")]
    public async Task<ActionResult<PagedResponse<AdminAccountResponse>>> ListUsers(
        [FromQuery] string? search, [FromQuery] string? role, [FromQuery] bool? isActive,
        [FromQuery] int page = 1, [FromQuery] int pageSize = Paging.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ListAccountsQuery(search, role, isActive, page, pageSize), cancellationToken);
        return Ok(new PagedResponse<AdminAccountResponse>(
            result.Items.Select(AdminAccountResponse.From).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    [EndpointDescription("Requires role: SuperAdmin. One account (safe fields only).")]
    [HttpGet("users/{id:guid}")]
    public async Task<ActionResult<AdminAccountResponse>> GetUser(Guid id, CancellationToken cancellationToken) =>
        Ok(AdminAccountResponse.From(await sender.Send(new GetAccountQuery(id), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin. Activates or deactivates an account; the role never changes. Deactivation revokes every session and makes existing access tokens fail immediately. Same state ⇒ 200 changed=false (no audit). Own account or the last active SuperAdmin ⇒ 409. Audited (AccountDeactivated / AccountReactivated).")]
    [HttpPut("users/{id:guid}/status")]
    public async Task<ActionResult<AccountStatusResponse>> SetUserStatus(Guid id, SetAccountStatusRequest request, CancellationToken cancellationToken) =>
        Ok(AccountStatusResponse.From(await sender.Send(new SetAccountStatusCommand(User.GetUserId(), id, request.IsActive), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin. Append-only administrative audit (Agent creation, account (de)activation, organization settings changes) since ADMIN-001. action; from/to (yyyy-MM-dd, UTC days, inclusive); search (actor/target id, actor name/email, target label); paging as /users. Newest first.")]
    [HttpGet("audit-logs")]
    public async Task<ActionResult<PagedResponse<AdminAuditLogResponse>>> ListAuditLogs(
        [FromQuery] AdminAuditAction? action, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = Paging.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ListAuditLogsQuery(action, from, to, search, page, pageSize), cancellationToken);
        return Ok(new PagedResponse<AdminAuditLogResponse>(
            result.Items.Select(AdminAuditLogResponse.From).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    [EndpointDescription("Requires role: SuperAdmin. Informational organization/support settings. isConfigured=false until first saved. Returns the ETag header.")]
    [HttpGet("organization-settings")]
    public async Task<ActionResult<OrganizationSettingsResponse>> GetSettings(CancellationToken cancellationToken) =>
        SettingsResult(await sender.Send(new GetOrganizationSettingsQuery(), cancellationToken));

    [EndpointDescription("Requires role: SuperAdmin. displayName required (≤ 150); supportEmail optional valid email (≤ 256); supportPhone optional (≤ 40); supportInformation optional (≤ 1000). Identical values ⇒ nothing saved, same version, no audit. Changes ⇒ one OrganizationSettingsUpdated audit entry." + Concurrency)]
    [HttpPut("organization-settings")]
    public async Task<ActionResult<OrganizationSettingsResponse>> UpdateSettings(UpdateOrganizationSettingsRequest request, CancellationToken cancellationToken) =>
        SettingsResult(await sender.Send(
            new UpdateOrganizationSettingsCommand(
                User.GetUserId(), EntityTags.RequireIfMatch(Request), request.DisplayName, request.SupportEmail, request.SupportPhone, request.SupportInformation),
            cancellationToken));

    private ActionResult<OrganizationSettingsResponse> SettingsResult(OrganizationSettingsDetails details)
    {
        Response.Headers.ETag = EntityTags.Format(details.Version);
        return Ok(OrganizationSettingsResponse.From(details));
    }
}
