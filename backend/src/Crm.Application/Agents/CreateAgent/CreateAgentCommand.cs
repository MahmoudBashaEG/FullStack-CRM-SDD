using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Messaging;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Domain.Administration;
using Crm.Domain.Identity;
using Mediator;

namespace Crm.Application.Agents.CreateAgent;

public sealed record CreateAgentCommand(
    Guid ActorUserId, string FirstName, string LastName, string Email, string? PhoneNumber, string Password)
    : ICommand<UserAccount>, ITransactionalCommand; // create user + add role + audit = one transaction

public sealed class CreateAgentCommandHandler(
    IIdentityService identity,
    IAdminAuditRepository audit,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CreateAgentCommand, UserAccount>
{
    public async ValueTask<UserAccount> Handle(CreateAgentCommand command, CancellationToken cancellationToken)
    {
        var email = command.Email.Trim();
        if (await identity.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var agent = await identity.CreateUserAsync(
            new NewUserAccount(email, command.FirstName.Trim(), command.LastName.Trim(), AccountRules.NormalizeOptional(command.PhoneNumber)),
            command.Password,
            UserRoles.Agent, // always server-side; the request has no role field
            cancellationToken);

        // Never the password: role and active state only (C7).
        audit.Add(AdminAuditEntry.AgentCreated(command.ActorUserId, agent.Id, agent.Email, timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return agent;
    }
}
