using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Mediator;

namespace Crm.Application.Auth.Login;

/// <summary>
/// NOT ITransactionalCommand on purpose: a failed attempt throws 401, and the failed-attempt
/// counter written by CheckCredentialsAsync must survive (a transaction would roll it back).
/// </summary>
public sealed record LoginCommand(string Email, string Password) : ICommand<AuthSession>;

public sealed class LoginCommandHandler(
    IIdentityService identity,
    AuthSessionFactory sessionFactory,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<LoginCommand, AuthSession>
{
    public const string InvalidCredentials = "Invalid email or password.";

    public async ValueTask<AuthSession> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await identity.CheckCredentialsAsync(command.Email.Trim(), command.Password, cancellationToken)
            ?? throw new UnauthorizedException(InvalidCredentials);

        var session = sessionFactory.Issue(user, familyId: Guid.CreateVersion7(), timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return session;
    }
}
