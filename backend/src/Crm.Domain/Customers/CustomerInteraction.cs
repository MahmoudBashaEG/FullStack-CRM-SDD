namespace Crm.Domain.Customers;

/// <summary>
/// Manually logged customer interaction (CUST-002). Append-only (C2 Option A). OccurredAtUtc = when the contact happened
/// (may be in the past); CreatedAtUtc = when it was recorded. RecordedByUserId is set by the server only.
/// </summary>
public sealed class CustomerInteraction
{
    private CustomerInteraction() { } // EF Core

    public long Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public CustomerInteractionType Type { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static CustomerInteraction Record(
        Guid customerId, CustomerInteractionType type, string summary, DateTimeOffset occurredAtUtc, Guid recordedByUserId, DateTimeOffset now) =>
        new()
        {
            CustomerId = customerId,
            Type = type,
            Summary = summary,
            OccurredAtUtc = occurredAtUtc,
            RecordedByUserId = recordedByUserId,
            CreatedAtUtc = now,
        };
}
