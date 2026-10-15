using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Messaging;
using Crm.Application.Abstractions.Persistence;
using Crm.Domain.Identity;
using Mediator;

namespace Crm.Application.Agents.SetAgentPassword;

/// <summary>SuperAdmin reset. No forced change on next login.</summary>
public sealed record SetAgentPasswordCommand(Guid Id, string NewPassword) : ICommand, ITransactionalCommand;

public sealed class SetAgentPasswordCommandHandler(
    IIdentityService identity,
    IRefreshSessionRepository sessions,
    TimeProvider timeProvider) : ICommandHandler<SetAgentPasswordCommand>
{
    public async ValueTask<Unit> Handle(SetAgentPasswordCommand command, CancellationToken cancellationToken)
    {
        var agent = await identity.GetAgentOrThrowAsync(command.Id, cancellationToken);
        await identity.SetPasswordAsync(agent.Id, command.NewPassword, cancellationToken);
        await sessions.RevokeAllForUserAsync(agent.Id, RefreshSessionRevocationReason.PasswordReset, timeProvider.GetUtcNow(), cancellationToken);
        return Unit.Value;
    }
}
