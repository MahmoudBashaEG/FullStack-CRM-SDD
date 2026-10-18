using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Domain.KnowledgeBase;
using FluentValidation;
using Mediator;

namespace Crm.Application.KnowledgeBase.UpdateArticle;

/// <summary>Content only — status, author and publication fields never change here (K1). Not visible ⇒ 404; see KnowledgeBaseGuards.EnsureCanEdit. Single save.</summary>
public sealed record UpdateKnowledgeArticleCommand(
    StaffActor Actor, Guid Id, string Title, string? Summary, string Body, KnowledgeArticleType? Type, Guid? CategoryId,
    KnowledgeArticleVisibility? Visibility) : ICommand<KnowledgeArticleDetails>;

public sealed class UpdateKnowledgeArticleCommandHandler(
    IKnowledgeBaseRepository knowledgeBase,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<UpdateKnowledgeArticleCommand, KnowledgeArticleDetails>
{
    public async ValueTask<KnowledgeArticleDetails> Handle(UpdateKnowledgeArticleCommand command, CancellationToken cancellationToken)
    {
        var article = await knowledgeBase.GetVisibleArticleAsync(command.Id, command.Actor, cancellationToken)
            ?? throw new NotFoundException($"Knowledge article '{command.Id}' was not found.");

        KnowledgeBaseGuards.EnsureCanEdit(article, command.Actor);

        // An unchanged category is accepted even if it was deactivated since; a changed one must be active.
        var categoryId = command.CategoryId!.Value; // validator: NotNull
        if (categoryId != article.CategoryId)
        {
            var category = await knowledgeBase.GetCategoryAsync(categoryId, cancellationToken);
            if (category is not { IsActive: true })
            {
                throw KnowledgeBaseGuards.Invalid("categoryId", "Choose an active category.");
            }
        }

        var changed = article.UpdateContent(
            command.Title.Trim(), KnowledgeBaseGuards.NormalizeOptional(command.Summary), command.Body.Trim(), command.Type!.Value,
            categoryId, command.Visibility!.Value, command.Actor.UserId, timeProvider.GetUtcNow());
        if (changed)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return (await knowledgeBase.FindArticleDetailsAsync(article.Id, command.Actor, cancellationToken))!;
    }
}

public sealed class UpdateKnowledgeArticleValidator : AbstractValidator<UpdateKnowledgeArticleCommand>
{
    public UpdateKnowledgeArticleValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Title).NotEmpty().MaximumLength(KnowledgeBaseRules.TitleMaxLength);
        RuleFor(c => c.Summary).MaximumLength(KnowledgeBaseRules.SummaryMaxLength);
        RuleFor(c => c.Body).NotEmpty().MaximumLength(KnowledgeBaseRules.BodyMaxLength);
        RuleFor(c => c.Type).NotNull().IsInEnum();
        RuleFor(c => c.CategoryId).NotNull().NotEqual(Guid.Empty);
        RuleFor(c => c.Visibility).NotNull().IsInEnum();
    }
}
