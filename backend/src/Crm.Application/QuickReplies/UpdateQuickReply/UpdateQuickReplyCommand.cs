using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Domain.Tickets;
using FluentValidation;
using Mediator;

namespace Crm.Application.QuickReplies.UpdateQuickReply;

/// <summary>Visible but not owned ⇒ 403 (Agent editing a Shared template). Archived ⇒ 409. Scope and owner never change. Single save.</summary>
public sealed record UpdateQuickReplyCommand(StaffActor Actor, Guid Id, string Title, string Body, TicketCategory? Category)
    : ICommand<QuickReplyDetails>;

public sealed class UpdateQuickReplyCommandHandler(
    IQuickReplyRepository templates,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<UpdateQuickReplyCommand, QuickReplyDetails>
{
    public async ValueTask<QuickReplyDetails> Handle(UpdateQuickReplyCommand command, CancellationToken cancellationToken)
    {
        var template = await templates.GetVisibleAsync(command.Id, command.Actor, cancellationToken)
            ?? throw new NotFoundException($"Quick reply '{command.Id}' was not found.");

        QuickReplyGuards.EnsureCanEdit(template, command.Actor);
        if (template.IsArchived)
        {
            throw new ConflictException("The quick reply is archived. Restore it first.");
        }

        if (template.Update(command.Title.Trim(), command.Body.Trim(), command.Category!.Value, command.Actor.UserId, timeProvider.GetUtcNow()))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return (await templates.FindDetailsByIdAsync(template.Id, command.Actor, cancellationToken))!;
    }
}

public sealed class UpdateQuickReplyValidator : AbstractValidator<UpdateQuickReplyCommand>
{
    public UpdateQuickReplyValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Title).NotEmpty().MaximumLength(QuickReplyRules.TitleMaxLength);
        RuleFor(c => c.Body).NotEmpty().MaximumLength(QuickReplyRules.BodyMaxLength);
        RuleFor(c => c.Category).NotNull().IsInEnum();
    }
}
