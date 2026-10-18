namespace Crm.Domain.Customers;

/// <summary>
/// Small staff-only customer file stored in SQL Server (C4). FileName is sanitized display metadata, never a path.
/// ContentType comes from the server's extension allow-list, never from the client. No delete/replace in CUST-002.
/// Content is only read by the download query; metadata queries never select it.
/// </summary>
public sealed class CustomerAttachment
{
    private CustomerAttachment() { } // EF Core

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public int SizeBytes { get; private set; }
    public byte[] Content { get; private set; } = [];
    public Guid UploadedByUserId { get; private set; }
    public DateTimeOffset UploadedAtUtc { get; private set; }

    public static CustomerAttachment Upload(
        Guid id, Guid customerId, string fileName, string contentType, byte[] content, Guid uploadedByUserId, DateTimeOffset now) =>
        new()
        {
            Id = id,
            CustomerId = customerId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = content.Length,
            Content = content,
            UploadedByUserId = uploadedByUserId,
            UploadedAtUtc = now,
        };
}
