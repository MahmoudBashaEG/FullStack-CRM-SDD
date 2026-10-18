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

    private sealed class FakeCustomerRecordRepository(Guid existingCustomerId) : ICustomerRecordRepository
    {
        public List<CustomerAttachment> Attachments { get; } = [];

        public Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(customerId == existingCustomerId);

        public void AddAttachment(CustomerAttachment attachment) => Attachments.Add(attachment);

        public Task<CustomerAttachmentDetails> GetAttachmentAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var a = Attachments.Single(x => x.Id == id);
            return Task.FromResult(new CustomerAttachmentDetails(
                a.Id, a.CustomerId, a.FileName, a.ContentType, a.SizeBytes, a.UploadedByUserId, "Test Agent", a.UploadedAtUtc));
        }

        public void AddNote(CustomerNote note) => throw new NotSupportedException();
        public Task<CustomerNoteDetails> GetNoteAsync(long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<CustomerNoteDetails>> ListNotesAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void AddInteraction(CustomerInteraction interaction) => throw new NotSupportedException();
        public Task<CustomerInteractionDetails> GetInteractionAsync(long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<CustomerInteractionDetails>> ListInteractionsAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<CustomerAttachmentDetails>> ListAttachmentsAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomerAttachmentFile?> FindAttachmentFileAsync(Guid customerId, Guid attachmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { SaveCount++; return Task.FromResult(1); }
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
