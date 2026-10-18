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
