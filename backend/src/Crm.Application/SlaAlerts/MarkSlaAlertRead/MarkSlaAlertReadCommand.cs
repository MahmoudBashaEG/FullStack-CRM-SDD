using Crm.Application.Abstractions.Persistence;
using Crm.Application.AgentTasks;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Mediator;

namespace Crm.Application.SlaAlerts.MarkSlaAlertRead;

/// <summary>
/// One conditional UPDATE (no IUnitOfWork): only the recipient who can still see the ticket can mark an alert read.
/// Already read = success. Never touches the ticket.
/// </summary>
public sealed record MarkSlaAlertReadCommand(StaffActor Actor, long Id) : ICommand;

public sealed class MarkSlaAlertReadCommandHandler(ISlaAlertRepository alerts, TimeProvider timeProvider)
    : ICommandHandler<MarkSlaAlertReadCommand>
{
    public async ValueTask<Unit> Handle(MarkSlaAlertReadCommand command, CancellationToken cancellationToken)
    {
        var outcome = await alerts.MarkReadAsync(command.Id, command.Actor, timeProvider.GetUtcNow(), cancellationToken);
        if (outcome == MarkReadOutcome.NotFound)
        {
            throw new NotFoundException($"SLA alert '{command.Id}' was not found.");
        }

        return Unit.Value;
    }
}
