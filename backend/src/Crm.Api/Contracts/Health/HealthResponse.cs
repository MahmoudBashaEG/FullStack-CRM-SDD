namespace Crm.Api.Contracts.Health;

/// <summary>JSON: { "status": "Healthy", "service": "Crm.Api", "checkedAtUtc": "..." }</summary>
public sealed record HealthResponse(string Status, string Service, DateTimeOffset CheckedAtUtc);
