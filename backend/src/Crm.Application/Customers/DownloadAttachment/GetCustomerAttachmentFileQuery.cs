using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Mediator;

namespace Crm.Application.Customers.DownloadAttachment;

public sealed record GetCustomerAttachmentFileQuery(Guid CustomerId, Guid AttachmentId) : IQuery<CustomerAttachmentFile>;

/// <summary>A missing customer and an attachment of another customer both give 404 — no separate existence query needed.</summary>
public sealed class GetCustomerAttachmentFileQueryHandler(ICustomerRecordRepository records)
    : IQueryHandler<GetCustomerAttachmentFileQuery, CustomerAttachmentFile>
{
    public async ValueTask<CustomerAttachmentFile> Handle(GetCustomerAttachmentFileQuery query, CancellationToken cancellationToken) =>
        await records.FindAttachmentFileAsync(query.CustomerId, query.AttachmentId, cancellationToken)
            ?? throw new NotFoundException($"Attachment '{query.AttachmentId}' was not found.");
}
