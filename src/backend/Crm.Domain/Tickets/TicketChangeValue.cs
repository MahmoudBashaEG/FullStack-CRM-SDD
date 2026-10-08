namespace Crm.Domain.Tickets;

public abstract record TicketChangeValue;
public sealed record TicketTextValue(string Value) : TicketChangeValue;
public sealed record TicketReferenceValue(Guid Id, string DisplayName) : TicketChangeValue;
public sealed record AgentSnapshot(Guid Id, string DisplayName);
public sealed record TicketCreationValue(string Reference, string Subject, string Description, CategorySnapshot Category, string Priority, string Status, AgentSnapshot? AssignedAgent) : TicketChangeValue;
