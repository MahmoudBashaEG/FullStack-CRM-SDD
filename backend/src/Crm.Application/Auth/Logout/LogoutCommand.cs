using Crm.Application.Abstractions.Persistence;
using Crm.Domain.Identity;
using Mediator;

namespace Crm.Application.Auth.Logout;

public sealed record LogoutCommand(string? RefreshToken) : ICommand;

public sealed class LogoutCommandHandler(IRefreshSessionRepository sessions, TimeProvider timeProvider)
    : ICommandHandler<LogoutCommand>
{
    public async ValueTask<Unit> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return Unit.Value;
        }

        // Revoke the whole login (family), including sibling sessions created by concurrent tabs.
        var session = await sessions.FindByTokenHashAsync(RefreshTokens.Hash(command.RefreshToken), cancellationToken);
        if (session is not null)
        {
            await sessions.RevokeFamilyAsync(session.FamilyId, RefreshSessionRevocationReason.Logout, timeProvider.GetUtcNow(), cancellationToken);
        }
        return Unit.Value;
    }
}
