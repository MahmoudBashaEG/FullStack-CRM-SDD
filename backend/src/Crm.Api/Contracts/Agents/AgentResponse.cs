using Crm.Application.Abstractions.Identity;

namespace Crm.Api.Contracts.Agents;

public sealed record AgentResponse(
    Guid Id, string Email, string FirstName, string LastName, string? PhoneNumber, bool IsActive, DateTimeOffset CreatedAtUtc)
{
    public static AgentResponse From(UserAccount user) =>
        new(user.Id, user.Email, user.FirstName, user.LastName, user.PhoneNumber, user.IsActive, user.CreatedAtUtc);
}
