using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Mediator;

namespace Crm.Application.QuickReplies.SetQuickReplyArchived;

/// <summary>Archive/restore. Repeating the current state is a no-op. No hard delete. Single save.</summary>
public sealed record SetQuickReplyArchivedCommand(StaffActor Actor, Guid Id, bool Archived) : ICommand<QuickReplyDetails>;

public sealed class SetQuickReplyArchivedCommandHandler(
    IQuickReplyRepository templates,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SetQuickReplyArchivedCommand, QuickReplyDetails>
{
    public async ValueTask<QuickReplyDetails> Handle(SetQuickReplyArchivedCommand command, CancellationToken cancellationToken)
    {
        var template = await templates.GetVisibleAsync(command.Id, command.Actor, cancellationToken)
            ?? throw new NotFoundException($"Quick reply '{command.Id}' was not found.");

        QuickReplyGuards.EnsureCanEdit(template, command.Actor);

        var now = timeProvider.GetUtcNow();
        var changed = command.Archived
            ? template.Archive(command.Actor.UserId, now)
            : template.Restore(command.Actor.UserId, now);
        if (changed)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return (await templates.FindDetailsByIdAsync(template.Id, command.Actor, cancellationToken))!;
    }
}
