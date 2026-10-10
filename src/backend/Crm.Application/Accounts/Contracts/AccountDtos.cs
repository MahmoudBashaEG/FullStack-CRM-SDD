using Crm.Domain.Accounts;
namespace Crm.Application.Accounts.Contracts;

public sealed record AccountIdentity(Guid UserId, string DisplayName, AccountRole Role);
public sealed record NewAccount(string DisplayName, string Email, string Password, AccountRole Role);
public sealed record AgentPage(IReadOnlyList<AgentAccountDto> Items,int Page,int PageSize,int TotalCount);
public sealed record AgentAccountDto(Guid UserId, string DisplayName, string Email, bool IsActive, Guid AssignmentReferenceId, string Version)
{
    public string Role => nameof(AccountRole.Agent);
}
