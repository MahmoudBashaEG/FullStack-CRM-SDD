using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Validation;
using Crm.Domain.QuickReplies;
using Crm.Domain.Tickets;
using FluentValidation;
using Mediator;

namespace Crm.Application.QuickReplies.CreateQuickReply;

/// <summary>Scope comes from the caller's role (Agent ⇒ Personal and owner; SuperAdmin ⇒ Shared). The command has no scope or owner. Single save.</summary>
public sealed record CreateQuickReplyCommand(StaffActor Actor, string Title, string Body, TicketCategory? Category)
    : ICommand<QuickReplyDetails>;

public sealed class CreateQuickReplyCommandHandler(
    IQuickReplyRepository templates,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CreateQuickReplyCommand, QuickReplyDetails>
{
    public async ValueTask<QuickReplyDetails> Handle(CreateQuickReplyCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var id = Guid.CreateVersion7();
        var title = command.Title.Trim();
        var body = command.Body.Trim();
        var category = command.Category!.Value; // validator: NotNull

        var template = command.Actor.IsSuperAdmin
            ? QuickReplyTemplate.CreateShared(id, command.Actor.UserId, title, body, category, now)
            : QuickReplyTemplate.CreatePersonal(id, command.Actor.UserId, title, body, category, now);

        templates.Add(template);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await templates.FindDetailsByIdAsync(id, command.Actor, cancellationToken))!;
    }
}

public sealed class CreateQuickReplyValidator : AbstractValidator<CreateQuickReplyCommand>
{
    public CreateQuickReplyValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(QuickReplyRules.TitleMaxLength);
        RuleFor(c => c.Body).NotEmpty().MaximumLength(QuickReplyRules.BodyMaxLength);
        RuleFor(c => c.Category).NotNull().IsInEnum();
    }
}
