using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Messaging;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Administration;
using Crm.Domain.Identity;
using FluentValidation;
using Mediator;

namespace Crm.Application.Administration.SetAccountStatus;

/// <summary>
/// ADMIN-001 AD1/AD3: activate or deactivate an account, with its audit entry in the same transaction.
/// RequiredRole = UserRoles.Agent for the legacy /agents/{id}/status route (other ids 404 there).
/// IsActive is nullable only so a missing JSON field fails validation instead of defaulting to false.
/// </summary>
public sealed record SetAccountStatusCommand(Guid ActorUserId, Guid UserId, bool? IsActive, string? RequiredRole = null)
    : ICommand<AccountStatusChange>, ITransactionalCommand; // lock + Identity save + session revocation + audit = one transaction

public sealed class SetAccountStatusCommandHandler(
    IAccountAdministration accounts,
    IIdentityService identity,
    IRefreshSessionRepository sessions,
    IAdminAuditRepository audit,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SetAccountStatusCommand, AccountStatusChange>
{
    public async ValueTask<AccountStatusChange> Handle(SetAccountStatusCommand command, CancellationToken cancellationToken)
    {
        var isActive = command.IsActive!.Value;

        // Serializes concurrent toggles of this account: the loser re-reads the committed state and becomes a no-op.
        var locked = await accounts.LockAccountAsync(command.UserId, cancellationToken);
        var account = locked is null ? null : await identity.FindByIdAsync(command.UserId, cancellationToken);
        if (account is null || (command.RequiredRole is { } role && account.Role != role))
        {
            throw new NotFoundException(command.RequiredRole == UserRoles.Agent
                ? $"Agent '{command.UserId}' was not found."
                : $"Account '{command.UserId}' was not found.");
        }

        if (account.IsActive == isActive)
        {
            return new AccountStatusChange(account, Changed: false); // idempotent: no stamp rotation, no revocation, no audit
        }

        if (!isActive)
        {
            if (account.Id == command.ActorUserId)
            {
                throw new ConflictException("You cannot deactivate your own account.");
            }

            if (account.Role == UserRoles.SuperAdmin)
            {
                var admins = await accounts.LockSuperAdminsAsync(cancellationToken);
                if (!admins.Any(a => a.IsActive && a.UserId != account.Id))
                {
                    throw new ConflictException("At least one other active SuperAdmin is required.");
                }
            }
        }

        var now = timeProvider.GetUtcNow();
        await identity.SetActiveAsync(account.Id, isActive, cancellationToken); // also rotates the security stamp ⇒ old JWTs fail
        if (!isActive)
        {
            await sessions.RevokeAllForUserAsync(account.Id, RefreshSessionRevocationReason.AccountDisabled, now, cancellationToken);
        }

        audit.Add(AdminAuditEntry.AccountStatusChanged(command.ActorUserId, account.Id, account.Email, isActive, now));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AccountStatusChange((await identity.FindByIdAsync(account.Id, cancellationToken))!, Changed: true);
    }
}

public sealed class SetAccountStatusValidator : AbstractValidator<SetAccountStatusCommand>
{
    public SetAccountStatusValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.IsActive).NotNull();
    }
}
