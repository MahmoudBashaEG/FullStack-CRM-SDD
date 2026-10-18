namespace Crm.Domain.Customers;

/// <summary>
/// Internal staff note on a CRM customer (CUST-002). Append-only (C2 Option A): no edit/delete methods exist —
/// a correction is a new note. Never visible to customers and unrelated to TicketActivity / ticket discussions.
/// Id is a store-generated identity (insertion order breaks ties). AuthorUserId is an Identity user id set by the server only.
/// </summary>
public sealed class CustomerNote
{
    private CustomerNote() { } // EF Core

    public long Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static CustomerNote Add(Guid customerId, Guid authorUserId, string body, DateTimeOffset now) =>
        new() { CustomerId = customerId, AuthorUserId = authorUserId, Body = body, CreatedAtUtc = now };
}
