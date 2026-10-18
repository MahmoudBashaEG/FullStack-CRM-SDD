using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using FluentValidation;
using Mediator;

namespace Crm.Application.KnowledgeBase.UpdateCategory;

/// <summary>SuperAdmin only. Rename and/or deactivate/reactivate. Saves only when something changed.</summary>
public sealed record UpdateKnowledgeCategoryCommand(StaffActor Actor, Guid Id, string Name, bool IsActive) : ICommand<KnowledgeCategoryDetails>;

public sealed class UpdateKnowledgeCategoryCommandHandler(
    IKnowledgeBaseRepository knowledgeBase,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<UpdateKnowledgeCategoryCommand, KnowledgeCategoryDetails>
{
    public async ValueTask<KnowledgeCategoryDetails> Handle(UpdateKnowledgeCategoryCommand command, CancellationToken cancellationToken)
    {
        KnowledgeBaseGuards.EnsureSuperAdmin(command.Actor);

        var category = await knowledgeBase.GetCategoryAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException($"Knowledge category '{command.Id}' was not found.");

        var name = command.Name.Trim();
        if (await knowledgeBase.CategoryNameExistsAsync(name, command.Id, cancellationToken))
        {
            throw new ConflictException("A category with this name already exists.");
        }

        if (category.Update(name, command.IsActive, command.Actor.UserId, timeProvider.GetUtcNow()))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await knowledgeBase.GetCategoryDetailsAsync(category.Id, cancellationToken);
    }
}

public sealed class UpdateKnowledgeCategoryValidator : AbstractValidator<UpdateKnowledgeCategoryCommand>
{
    public UpdateKnowledgeCategoryValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(KnowledgeBaseRules.CategoryNameMaxLength);
    }
}
