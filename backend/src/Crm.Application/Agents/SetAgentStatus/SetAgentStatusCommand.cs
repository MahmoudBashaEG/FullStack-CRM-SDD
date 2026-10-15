using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Messaging;
using Crm.Application.Abstractions.Persistence;
using Crm.Domain.Identity;
using Mediator;

namespace Crm.Application.Agents.SetAgentStatus;

public sealed record SetAgentStatusCommand(Guid Id, bool IsActive) : ICommand, ITransactionalCommand;

public sealed class SetAgentStatusCommandHandler(
    IIdentityService identity,
    IRefreshSessionRepository sessions,
    TimeProvider timeProvider) : ICommandHandler<SetAgentStatusCommand>
{
    public async ValueTask<Unit> Handle(SetAgentStatusCommand command, CancellationToken cancellationToken)
    {
        var agent = await identity.GetAgentOrThrowAsync(command.Id, cancellationToken);
        await identity.SetActiveAsync(agent.Id, command.IsActive, cancellationToken);

        if (!command.IsActive)
        {
            await sessions.RevokeAllForUserAsync(agent.Id, RefreshSessionRevocationReason.AccountDisabled, timeProvider.GetUtcNow(), cancellationToken);
        }

        return Unit.Value;
    }
}
