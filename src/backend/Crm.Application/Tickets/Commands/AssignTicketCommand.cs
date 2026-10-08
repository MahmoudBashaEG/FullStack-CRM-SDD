using Crm.Application.Common;
using Crm.Application.Tickets.Contracts;
using Crm.Application.Tickets.Ports;
using MediatR;
using Crm.Application.Tickets.Validation;
using Crm.Domain.Tickets;
namespace Crm.Application.Tickets.Commands;

public sealed record AssignTicketCommand(string Reference, string? ExpectedVersion, Guid? AgentId) : IRequest<OperationResult<TicketDetailDto>>;
public sealed class AssignTicketHandler(ITicketMutationStore store, ICurrentActor actor, TimeProvider clock) : IRequestHandler<AssignTicketCommand, OperationResult<TicketDetailDto>>
{
    public Task<OperationResult<TicketDetailDto>> Handle(AssignTicketCommand request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var failure = TicketMutationValidators.Version(request.ExpectedVersion, out var version);
        if (failure is not null) return Task.FromResult(new OperationResult<TicketDetailDto>(null, failure));
        var trusted = actor.Get();
        return store.MutateAsync(request.Reference, version, async (scope, cancellation) =>
        {
            if (scope.Ticket.Status == TicketStatus.Closed) return new("ticket_closed", 409);
            var agent = scope.Agent;
            if (request.AgentId is null) agent = null;
            else if (request.AgentId != scope.Ticket.AssignedAgentId)
            {
                agent = await scope.SelectAgent(request.AgentId.Value, cancellation);
                if (agent is null || !agent.IsEligible) return new("agent_ineligible", 409);
            }
            scope.Ticket.Assign(scope.Agent is null ? null : new(scope.Agent.Id, scope.Agent.DisplayName), agent is null ? null : new(agent.Id, agent.DisplayName),
                new(trusted.Issuer, trusted.Subject, trusted.DisplayName), clock.GetUtcNow());
            return null;
        }, token);
    }
}
