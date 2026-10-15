using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Messaging;
using Crm.Application.Abstractions.Persistence;
using Crm.Domain.Identity;
using FluentValidation;
using FluentValidation.Results;
using Mediator;

namespace Crm.Application.Profile.ChangeMyPassword;

public sealed record ChangeMyPasswordCommand(Guid UserId, string CurrentPassword, string NewPassword)
    : ICommand, ITransactionalCommand;

public sealed class ChangeMyPasswordCommandHandler(
    IIdentityService identity,
    IRefreshSessionRepository sessions,
    TimeProvider timeProvider) : ICommandHandler<ChangeMyPasswordCommand>
{
    public async ValueTask<Unit> Handle(ChangeMyPasswordCommand command, CancellationToken cancellationToken)
    {
        if (!await identity.ChangePasswordAsync(command.UserId, command.CurrentPassword, command.NewPassword, cancellationToken))
        {
            throw new ValidationException(
                [new ValidationFailure(nameof(ChangeMyPasswordCommand.CurrentPassword), "Current password is incorrect.")]);
        }

        await sessions.RevokeAllForUserAsync(command.UserId, RefreshSessionRevocationReason.PasswordChanged, timeProvider.GetUtcNow(), cancellationToken);
        return Unit.Value;
    }
}
