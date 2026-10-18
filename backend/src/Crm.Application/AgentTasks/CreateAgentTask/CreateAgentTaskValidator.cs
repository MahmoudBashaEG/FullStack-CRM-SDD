using Crm.Application.Common.Validation;
using FluentValidation;

namespace Crm.Application.AgentTasks.CreateAgentTask;

public sealed class CreateAgentTaskValidator : AbstractValidator<CreateAgentTaskCommand>
{
    public CreateAgentTaskValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(AgentTaskRules.TitleMaxLength);
        RuleFor(c => c.Description).MaximumLength(AgentTaskRules.DescriptionMaxLength);
        RuleFor(c => c.AssignedAgentUserId).NotEqual(Guid.Empty);
        RuleFor(c => c.TicketId).NotEqual(Guid.Empty);
    }
}
