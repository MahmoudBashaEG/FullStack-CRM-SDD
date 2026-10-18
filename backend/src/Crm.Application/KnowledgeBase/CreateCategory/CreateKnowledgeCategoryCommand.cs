using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Domain.KnowledgeBase;
using FluentValidation;
using Mediator;

namespace Crm.Application.KnowledgeBase.CreateCategory;

/// <summary>SuperAdmin only. Names are unique (case-insensitive). Single save.</summary>
public sealed record CreateKnowledgeCategoryCommand(StaffActor Actor, string Name) : ICommand<KnowledgeCategoryDetails>;

public sealed class CreateKnowledgeCategoryCommandHandler(
    IKnowledgeBaseRepository knowledgeBase,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CreateKnowledgeCategoryCommand, KnowledgeCategoryDetails>
{
    public async ValueTask<KnowledgeCategoryDetails> Handle(CreateKnowledgeCategoryCommand command, CancellationToken cancellationToken)
    {
        KnowledgeBaseGuards.EnsureSuperAdmin(command.Actor);

        var name = command.Name.Trim();
        if (await knowledgeBase.CategoryNameExistsAsync(name, null, cancellationToken))
        {
            throw new ConflictException("A category with this name already exists.");
        }

        var category = KnowledgeCategory.Create(Guid.CreateVersion7(), name, command.Actor.UserId, timeProvider.GetUtcNow());
        knowledgeBase.AddCategory(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await knowledgeBase.GetCategoryDetailsAsync(category.Id, cancellationToken);
    }
}

public sealed class CreateKnowledgeCategoryValidator : AbstractValidator<CreateKnowledgeCategoryCommand>
{
    public CreateKnowledgeCategoryValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(KnowledgeBaseRules.CategoryNameMaxLength);
    }
}
