using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Domain.KnowledgeBase;
using FluentValidation;
using Mediator;

namespace Crm.Application.KnowledgeBase.ChangeArticleStatus;

public enum KnowledgeArticleAction
{
    Submit = 0,
    Return = 1,
    Publish = 2,
    Archive = 3,
    Restore = 4,
}

/// <summary>
/// One handler for the K1 workflow. Submit: author (Agent) or SuperAdmin (any Draft, I2). Return/Publish/Archive/Restore: SuperAdmin only
/// (controller policy + handler backstop ⇒ 403). Not visible ⇒ 404. Illegal from the current status ⇒ 409. Single save.
/// </summary>
public sealed record ChangeKnowledgeArticleStatusCommand(StaffActor Actor, Guid Id, KnowledgeArticleAction Action, string? Reason = null)
    : ICommand<KnowledgeArticleDetails>;

public sealed class ChangeKnowledgeArticleStatusCommandHandler(
    IKnowledgeBaseRepository knowledgeBase,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ChangeKnowledgeArticleStatusCommand, KnowledgeArticleDetails>
{
    public async ValueTask<KnowledgeArticleDetails> Handle(ChangeKnowledgeArticleStatusCommand command, CancellationToken cancellationToken)
    {
        var article = await knowledgeBase.GetVisibleArticleAsync(command.Id, command.Actor, cancellationToken)
            ?? throw new NotFoundException($"Knowledge article '{command.Id}' was not found.");

        if (command.Action != KnowledgeArticleAction.Submit)
        {
            KnowledgeBaseGuards.EnsureSuperAdmin(command.Actor);
        }

        var target = command.Action switch
        {
            KnowledgeArticleAction.Submit => KnowledgeArticleStatus.ReadyForPublication,
            KnowledgeArticleAction.Return => KnowledgeArticleStatus.Draft,
            KnowledgeArticleAction.Publish => KnowledgeArticleStatus.Published,
            KnowledgeArticleAction.Archive => KnowledgeArticleStatus.Archived,
            KnowledgeArticleAction.Restore => KnowledgeArticleStatus.Draft,
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };

        // Return and Restore both target Draft; distinguish them by the required current status.
        var expectedFrom = command.Action switch
        {
            KnowledgeArticleAction.Return => KnowledgeArticleStatus.ReadyForPublication,
            KnowledgeArticleAction.Restore => KnowledgeArticleStatus.Archived,
            _ => article.Status,
        };
        if (article.Status != expectedFrom || !KnowledgeArticleWorkflow.IsAllowed(article.Status, target))
        {
            throw new ConflictException(ConflictMessage(command.Action));
        }

        var now = timeProvider.GetUtcNow();
        var actorId = command.Actor.UserId;
        switch (command.Action)
        {
            case KnowledgeArticleAction.Submit: article.Submit(actorId, now); break;
            case KnowledgeArticleAction.Return: article.ReturnToDraft(KnowledgeBaseGuards.NormalizeOptional(command.Reason), actorId, now); break;
            case KnowledgeArticleAction.Publish: article.Publish(actorId, now); break;
            case KnowledgeArticleAction.Archive: article.Archive(actorId, now); break;
            case KnowledgeArticleAction.Restore: article.Restore(actorId, now); break;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await knowledgeBase.FindArticleDetailsAsync(article.Id, command.Actor, cancellationToken))!;
    }

    private static string ConflictMessage(KnowledgeArticleAction action) => action switch
    {
        KnowledgeArticleAction.Submit => "Only a Draft article can be submitted for publication.",
        KnowledgeArticleAction.Return => "Only an article that is Ready for Publication can be returned for revision.",
        KnowledgeArticleAction.Publish => "Only an article that is Ready for Publication can be published.",
        KnowledgeArticleAction.Archive => "The article is already archived.",
        _ => "Only an archived article can be restored.",
    };
}

public sealed class ChangeKnowledgeArticleStatusValidator : AbstractValidator<ChangeKnowledgeArticleStatusCommand>
{
    public ChangeKnowledgeArticleStatusValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Action).IsInEnum();
        RuleFor(c => c.Reason).MaximumLength(KnowledgeBaseRules.ReturnReasonMaxLength);
    }
}
