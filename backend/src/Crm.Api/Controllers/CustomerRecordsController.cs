using Crm.Api.Authentication;
using Crm.Api.Contracts.Common;
using Crm.Api.Contracts.Customers;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Validation;
using Crm.Application.Customers.AddNote;
using Crm.Application.Customers.DownloadAttachment;
using Crm.Application.Customers.ListAttachments;
using Crm.Application.Customers.ListInteractions;
using Crm.Application.Customers.ListNotes;
using Crm.Application.Customers.LogInteraction;
using Crm.Application.Customers.UploadAttachment;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

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

    [EndpointDescription("Requires role: SuperAdmin or Agent. Manually logged interactions (PhoneCall, Meeting, Other), newest occurrence first. One-based page (default 1), pageSize (default 20, clamped to 100)." + Boundary)]
    [HttpGet("interactions")]
    public async Task<ActionResult<PagedResponse<CustomerInteractionResponse>>> Interactions(
        Guid customerId, [FromQuery] int page = 1, [FromQuery] int pageSize = Paging.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ListCustomerInteractionsQuery(customerId, page, pageSize), cancellationToken);
        return Ok(new PagedResponse<CustomerInteractionResponse>(
            result.Items.Select(CustomerInteractionResponse.From).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    [EndpointDescription("Requires role: SuperAdmin or Agent. Summary 1-500 characters; occurredAtUtc optional (default now), not in the future. Append-only. Not a message to the customer." + Boundary)]
    [HttpPost("interactions")]
    [ProducesResponseType<CustomerInteractionResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CustomerInteractionResponse>> LogInteraction(
        Guid customerId, LogCustomerInteractionRequest request, CancellationToken cancellationToken)
    {
        var interaction = await sender.Send(
            new LogCustomerInteractionCommand(customerId, User.GetUserId(), request.Type, request.Summary, request.OccurredAtUtc), cancellationToken);
        return CreatedAtAction(nameof(Interactions), new { customerId }, CustomerInteractionResponse.From(interaction));
    }

    [EndpointDescription("Requires role: SuperAdmin or Agent. Attachment metadata (no bytes), newest first. One-based page (default 1), pageSize (default 20, clamped to 100)." + Boundary)]
    [HttpGet("attachments")]
    public async Task<ActionResult<PagedResponse<CustomerAttachmentResponse>>> Attachments(
        Guid customerId, [FromQuery] int page = 1, [FromQuery] int pageSize = Paging.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ListCustomerAttachmentsQuery(customerId, page, pageSize), cancellationToken);
        return Ok(new PagedResponse<CustomerAttachmentResponse>(
            result.Items.Select(CustomerAttachmentResponse.From).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

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
