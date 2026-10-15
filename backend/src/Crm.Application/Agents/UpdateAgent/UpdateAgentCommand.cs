using Crm.Application.Abstractions.Identity;
using Crm.Application.Common.Validation;
using Mediator;

namespace Crm.Application.Agents.UpdateAgent;

/// <summary>Email is not editable in this story.</summary>
public sealed record UpdateAgentCommand(Guid Id, string FirstName, string LastName, string? PhoneNumber)
    : ICommand<UserAccount>;

public sealed class UpdateAgentCommandHandler(IIdentityService identity)
    : ICommandHandler<UpdateAgentCommand, UserAccount>
{
    public async ValueTask<UserAccount> Handle(UpdateAgentCommand command, CancellationToken cancellationToken)
    {
        var agent = await identity.GetAgentOrThrowAsync(command.Id, cancellationToken);
        return await identity.UpdateProfileAsync(
            agent.Id,
            new UserProfileUpdate(command.FirstName.Trim(), command.LastName.Trim(), AccountRules.NormalizeOptional(command.PhoneNumber)),
            cancellationToken);
    }
}
