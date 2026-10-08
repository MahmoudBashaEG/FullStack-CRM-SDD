using Crm.Application.Common;
using Crm.Application.Tickets.Contracts;
using Crm.Application.Tickets.Ports;
using MediatR;
using Crm.Application.Tickets.Validation;
using Crm.Domain.Tickets;
namespace Crm.Application.Tickets.Commands;

public sealed record UpdateTicketDetailsCommand(string Reference, string? ExpectedVersion, DetailPatch Patch) : IRequest<OperationResult<TicketDetailDto>>;
public sealed class UpdateTicketDetailsHandler(ITicketMutationStore store, ICurrentActor actor, TimeProvider clock) : IRequestHandler<UpdateTicketDetailsCommand, OperationResult<TicketDetailDto>>
{
    public Task<OperationResult<TicketDetailDto>> Handle(UpdateTicketDetailsCommand request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var failure = TicketMutationValidators.Version(request.ExpectedVersion, out var version) ?? TicketMutationValidators.Details(request.Patch);
        if (failure is not null) return Task.FromResult(new OperationResult<TicketDetailDto>(null, failure));
        var trusted = actor.Get();
        return store.MutateAsync(request.Reference, version, async (scope, cancellation) =>
        {
            if (scope.Ticket.Status == TicketStatus.Closed) return new("ticket_closed", 409);
            var category = scope.Category;
            if (request.Patch.CategoryId is Guid id && id != scope.Ticket.CategoryId)
            {
                var selected = await scope.SelectCategory(id, cancellation);
                if (selected is null || !selected.IsAvailable) return new("category_unavailable", 409);
                category = selected;
            }
            scope.Ticket.UpdateDetails(request.Patch.Subject ?? scope.Ticket.Subject, request.Patch.Description ?? scope.Ticket.Description,
                new(scope.Category.Id, scope.Category.DisplayName), new(category.Id, category.DisplayName),
                request.Patch.Priority is null ? scope.Ticket.Priority : Enum.Parse<TicketPriority>(request.Patch.Priority),
                new(trusted.Issuer, trusted.Subject, trusted.DisplayName), clock.GetUtcNow());
            return null;
        }, token);
    }
}
