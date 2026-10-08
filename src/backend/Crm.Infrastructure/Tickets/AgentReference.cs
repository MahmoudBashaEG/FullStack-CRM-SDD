namespace Crm.Infrastructure.Tickets;

public sealed class AgentReference
{
    public Guid Id { get; set; }
    public string Issuer { get; set; } = "";
    public string Subject { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsEligible { get; set; }
}
