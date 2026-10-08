using Crm.Application.Common;
using Crm.Application.Tickets.Contracts;
using Crm.Application.Tickets.Ports;
using Crm.Application.Tickets.Validation;
using Crm.Domain.Tickets;
using MediatR;
namespace Crm.Application.Tickets.Commands;

public sealed record ChangeTicketStatusCommand(string Reference, string? ExpectedVersion, string? Status) : IRequest<OperationResult<TicketDetailDto>>;
public sealed class ChangeTicketStatusHandler(ITicketMutationStore store, ICurrentActor actor, TimeProvider clock) : IRequestHandler<ChangeTicketStatusCommand, OperationResult<TicketDetailDto>>
{
    public Task<OperationResult<TicketDetailDto>> Handle(ChangeTicketStatusCommand request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var failure = TicketMutationValidators.Version(request.ExpectedVersion, out var version);
        if (failure is not null) return Task.FromResult(new OperationResult<TicketDetailDto>(null, failure));
        failure = ChangeTicketStatusValidator.Validate(request.Status, out var status);
        if (failure is not null) return Task.FromResult(new OperationResult<TicketDetailDto>(null, failure));
        var trusted = actor.Get();
        return store.MutateAsync(request.Reference, version, (scope, cancellation) =>
        {
            cancellation.ThrowIfCancellationRequested();
            try
            {
                scope.Ticket.ChangeStatus(status, new(trusted.Issuer, trusted.Subject, trusted.DisplayName), clock.GetUtcNow());
                return Task.FromResult<Failure?>(null);
            }
            catch (InvalidTicketTransitionException) { return Task.FromResult<Failure?>(new("invalid_transition", 409)); }
        }, token);
    }
}
