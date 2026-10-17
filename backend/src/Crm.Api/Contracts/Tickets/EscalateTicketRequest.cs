namespace Crm.Api.Contracts.Tickets;

/// <summary>Required, at most 500 characters, trimmed.</summary>
public sealed record EscalateTicketRequest(string Reason);
