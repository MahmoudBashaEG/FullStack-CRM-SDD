using Crm.Application.Common;
using Crm.Application.Tickets.Contracts;
using Crm.Application.Tickets.Ports;
using MediatR;
using Crm.Application.Tickets.Validation;
using Crm.Domain.Tickets;
namespace Crm.Application.Tickets.Commands;

public sealed record CreateTicketCommand(string? Subject, string? Description, Guid? CategoryId, string? Priority) : IRequest<OperationResult<TicketDetailDto>>;
public sealed class CreateTicketHandler(ITicketMutationStore store, ICurrentActor actor, TimeProvider clock) : IRequestHandler<CreateTicketCommand, OperationResult<TicketDetailDto>>
{
    public Task<OperationResult<TicketDetailDto>> Handle(CreateTicketCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var failure = CreateTicketValidator.Validate(request);
        if (failure is not null) return Task.FromResult(new OperationResult<TicketDetailDto>(null, failure));
        var trusted = actor.Get(); var now = clock.GetUtcNow();
        var priority = request.Priority is null ? TicketPriority.Normal : Enum.Parse<TicketPriority>(request.Priority);
        return store.CreateAsync(request.CategoryId ?? TicketDefaults.CategoryId,
            category => category is null || !category.IsAvailable
                ? OperationResult<Ticket>.Fail("category_unavailable", 409)
                : OperationResult<Ticket>.Success(Ticket.Create(request.Subject!, request.Description!, new(category.Id, category.DisplayName), priority, new(trusted.Issuer, trusted.Subject, trusted.DisplayName), now)), cancellationToken);
    }
}
