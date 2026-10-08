namespace Crm.Application.Tickets.Contracts;

public sealed record ReferenceChoice(Guid Id, string DisplayName);
public static class TicketDefaults
{
    public static readonly Guid CategoryId = Guid.Parse("00000000-0000-0000-0000-000000000001");
}
