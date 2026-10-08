namespace Crm.Application.Tickets.Contracts;
public sealed record DetailPatch(string? Subject = null, string? Description = null, Guid? CategoryId = null, string? Priority = null);
