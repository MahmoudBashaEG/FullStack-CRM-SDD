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
