using Crm.Application.Customers;
using Crm.Domain.Customers;

namespace Crm.Api.Contracts.Customers;

public sealed record AddCustomerNoteRequest(string Body);

public sealed record CustomerNoteResponse(long Id, Guid AuthorUserId, string AuthorName, string Body, DateTimeOffset CreatedAtUtc)
{
    public static CustomerNoteResponse From(CustomerNoteDetails n) => new(n.Id, n.AuthorUserId, n.AuthorName, n.Body, n.CreatedAtUtc);
}

/// <summary>occurredAtUtc omitted/null = now. type: "PhoneCall" | "Meeting" | "Other".</summary>
public sealed record LogCustomerInteractionRequest(CustomerInteractionType? Type, string Summary, DateTimeOffset? OccurredAtUtc);

public sealed record CustomerInteractionResponse(
    long Id, CustomerInteractionType Type, string Summary, DateTimeOffset OccurredAtUtc,
    Guid RecordedByUserId, string RecordedByName, DateTimeOffset CreatedAtUtc)
{
    public static CustomerInteractionResponse From(CustomerInteractionDetails i) =>
        new(i.Id, i.Type, i.Summary, i.OccurredAtUtc, i.RecordedByUserId, i.RecordedByName, i.CreatedAtUtc);
}

/// <summary>Metadata only. Bytes are served exclusively by GET …/attachments/{id}/content.</summary>
public sealed record CustomerAttachmentResponse(
    Guid Id, string FileName, string ContentType, int SizeBytes, Guid UploadedByUserId, string UploadedByName, DateTimeOffset UploadedAtUtc)
{
    public static CustomerAttachmentResponse From(CustomerAttachmentDetails a) =>
        new(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedByUserId, a.UploadedByName, a.UploadedAtUtc);
}
