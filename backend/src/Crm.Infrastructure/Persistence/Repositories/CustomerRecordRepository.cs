using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using Crm.Application.Customers;
using Crm.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

internal sealed class CustomerRecordRepository(CrmDbContext dbContext) : ICustomerRecordRepository
{
    public Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        dbContext.Customers.AnyAsync(c => c.Id == customerId, cancellationToken);

    public void AddNote(CustomerNote note) => dbContext.CustomerNotes.Add(note);

    public Task<CustomerNoteDetails> GetNoteAsync(long id, CancellationToken cancellationToken = default) =>
        (from n in dbContext.CustomerNotes.AsNoTracking().Where(n => n.Id == id)
         join author in dbContext.Users on n.AuthorUserId equals author.Id
         select new CustomerNoteDetails(n.Id, n.CustomerId, n.AuthorUserId, author.FirstName + " " + author.LastName, n.Body, n.CreatedAtUtc))
        .SingleAsync(cancellationToken);

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

    public void AddInteraction(CustomerInteraction interaction) => dbContext.CustomerInteractions.Add(interaction);

    public Task<CustomerInteractionDetails> GetInteractionAsync(long id, CancellationToken cancellationToken = default) =>
        (from i in dbContext.CustomerInteractions.AsNoTracking().Where(i => i.Id == id)
         join recorder in dbContext.Users on i.RecordedByUserId equals recorder.Id
         select new CustomerInteractionDetails(
             i.Id, i.CustomerId, i.Type, i.Summary, i.OccurredAtUtc, i.RecordedByUserId, recorder.FirstName + " " + recorder.LastName, i.CreatedAtUtc))
        .SingleAsync(cancellationToken);

    public async Task<PagedResult<CustomerInteractionDetails>> ListInteractionsAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var interactions = dbContext.CustomerInteractions.AsNoTracking().Where(i => i.CustomerId == customerId);
        var totalCount = await interactions.CountAsync(cancellationToken);
        var items = await (from i in interactions
                           join recorder in dbContext.Users on i.RecordedByUserId equals recorder.Id
                           orderby i.OccurredAtUtc descending, i.Id descending
                           select new CustomerInteractionDetails(
                               i.Id, i.CustomerId, i.Type, i.Summary, i.OccurredAtUtc, i.RecordedByUserId, recorder.FirstName + " " + recorder.LastName, i.CreatedAtUtc))
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<CustomerInteractionDetails>(items, page, pageSize, totalCount);
    }

    public void AddAttachment(CustomerAttachment attachment) => dbContext.CustomerAttachments.Add(attachment);

    // Metadata projections only: Content (varbinary(max)) is never selected here.
    public Task<CustomerAttachmentDetails> GetAttachmentAsync(Guid id, CancellationToken cancellationToken = default) =>
        (from a in dbContext.CustomerAttachments.AsNoTracking().Where(a => a.Id == id)
         join uploader in dbContext.Users on a.UploadedByUserId equals uploader.Id
         select new CustomerAttachmentDetails(
             a.Id, a.CustomerId, a.FileName, a.ContentType, a.SizeBytes, a.UploadedByUserId, uploader.FirstName + " " + uploader.LastName, a.UploadedAtUtc))
        .SingleAsync(cancellationToken);

    public async Task<PagedResult<CustomerAttachmentDetails>> ListAttachmentsAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var attachments = dbContext.CustomerAttachments.AsNoTracking().Where(a => a.CustomerId == customerId);
        var totalCount = await attachments.CountAsync(cancellationToken);
        var items = await (from a in attachments
                           join uploader in dbContext.Users on a.UploadedByUserId equals uploader.Id
                           orderby a.UploadedAtUtc descending, a.Id descending
                           select new CustomerAttachmentDetails(
                               a.Id, a.CustomerId, a.FileName, a.ContentType, a.SizeBytes, a.UploadedByUserId, uploader.FirstName + " " + uploader.LastName, a.UploadedAtUtc))
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<CustomerAttachmentDetails>(items, page, pageSize, totalCount);
    }

    public Task<CustomerAttachmentFile?> FindAttachmentFileAsync(Guid customerId, Guid attachmentId, CancellationToken cancellationToken = default) =>
        dbContext.CustomerAttachments.AsNoTracking()
            .Where(a => a.Id == attachmentId && a.CustomerId == customerId)
            .Select(a => new CustomerAttachmentFile(a.FileName, a.ContentType, a.Content))
            .SingleOrDefaultAsync(cancellationToken);
}
