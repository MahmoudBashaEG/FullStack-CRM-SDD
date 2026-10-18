using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Validation;
using Crm.Domain.KnowledgeBase;
using FluentValidation;
using Mediator;

namespace Crm.Application.KnowledgeBase.CreateArticle;

/// <summary>Always creates a Draft owned by the caller (K1). The command has no status or author. Single save.</summary>
public sealed record CreateKnowledgeArticleCommand(
    StaffActor Actor, string Title, string? Summary, string Body, KnowledgeArticleType? Type, Guid? CategoryId,
    KnowledgeArticleVisibility? Visibility) : ICommand<KnowledgeArticleDetails>;

public sealed class CreateKnowledgeArticleCommandHandler(
    IKnowledgeBaseRepository knowledgeBase,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CreateKnowledgeArticleCommand, KnowledgeArticleDetails>
{
    public async ValueTask<KnowledgeArticleDetails> Handle(CreateKnowledgeArticleCommand command, CancellationToken cancellationToken)
    {
        var categoryId = command.CategoryId!.Value; // validator: NotNull
        var category = await knowledgeBase.GetCategoryAsync(categoryId, cancellationToken);
        if (category is not { IsActive: true })
        {
            throw KnowledgeBaseGuards.Invalid("categoryId", "Choose an active category.");
        }

        var id = Guid.CreateVersion7();
        var article = KnowledgeArticle.CreateDraft(
            id, command.Actor.UserId, command.Title.Trim(), KnowledgeBaseGuards.NormalizeOptional(command.Summary), command.Body.Trim(),
            command.Type!.Value, categoryId, command.Visibility!.Value, timeProvider.GetUtcNow());

        knowledgeBase.AddArticle(article);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await knowledgeBase.FindArticleDetailsAsync(id, command.Actor, cancellationToken))!;
    }
}

public sealed class CreateKnowledgeArticleValidator : AbstractValidator<CreateKnowledgeArticleCommand>
{
    public CreateKnowledgeArticleValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(KnowledgeBaseRules.TitleMaxLength);
        RuleFor(c => c.Summary).MaximumLength(KnowledgeBaseRules.SummaryMaxLength);
        RuleFor(c => c.Body).NotEmpty().MaximumLength(KnowledgeBaseRules.BodyMaxLength);
        RuleFor(c => c.Type).NotNull().IsInEnum();
        RuleFor(c => c.CategoryId).NotNull().NotEqual(Guid.Empty);
        RuleFor(c => c.Visibility).NotNull().IsInEnum();
    }
}
