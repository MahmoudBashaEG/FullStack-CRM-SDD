using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Messaging;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Domain.Identity;
using Mediator;

namespace Crm.Application.Agents.CreateAgent;

public sealed record CreateAgentCommand(string FirstName, string LastName, string Email, string? PhoneNumber, string Password)
    : ICommand<UserAccount>, ITransactionalCommand; // create user + add role = two saves

public sealed class CreateAgentCommandHandler(IIdentityService identity)
    : ICommandHandler<CreateAgentCommand, UserAccount>
{
    public async ValueTask<UserAccount> Handle(CreateAgentCommand command, CancellationToken cancellationToken)
    {
        var email = command.Email.Trim();
        if (await identity.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        return await identity.CreateUserAsync(
            new NewUserAccount(email, command.FirstName.Trim(), command.LastName.Trim(), AccountRules.NormalizeOptional(command.PhoneNumber)),
            command.Password,
            UserRoles.Agent,
            cancellationToken);
    }
}
