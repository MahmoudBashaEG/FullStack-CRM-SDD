namespace Crm.Infrastructure.Tickets;

public sealed class CategoryReference
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = "";
    public bool IsAvailable { get; set; }
}
