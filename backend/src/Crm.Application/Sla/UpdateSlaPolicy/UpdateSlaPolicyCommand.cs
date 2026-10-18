using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Validation;
using Crm.Application.Sla.GetSlaPolicy;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;
using FluentValidation;
using Mediator;

namespace Crm.Application.Sla.UpdateSlaPolicy;

/// <summary>Creates or replaces the singleton policy. Single save — not transactional. Applies to tickets created afterwards only.</summary>
public sealed record UpdateSlaPolicyCommand(Guid ActorUserId, bool? IsEnabled, IReadOnlyList<SlaTargetInput>? Targets)
    : ICommand<SlaPolicyDetails>;

/// <summary>Priority is nullable only so a missing JSON field fails validation instead of defaulting to Low.</summary>
public sealed record SlaTargetInput(TicketPriority? Priority, int FirstResponseMinutes, int ResolutionMinutes);

public sealed class UpdateSlaPolicyCommandHandler(
    ISlaConfigurationRepository configuration,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<UpdateSlaPolicyCommand, SlaPolicyDetails>
{
    public async ValueTask<SlaPolicyDetails> Handle(UpdateSlaPolicyCommand command, CancellationToken cancellationToken)
    {
        var targets = command.Targets!
            .Select(t => new SlaTarget(t.Priority!.Value, t.FirstResponseMinutes, t.ResolutionMinutes))
            .ToList();
        var now = timeProvider.GetUtcNow();

        var policy = await configuration.FindPolicyAsync(cancellationToken);
        if (policy is null)
        {
            policy = SlaPolicy.Create(command.IsEnabled!.Value, targets, command.ActorUserId, now);
            configuration.Add(policy);
        }
        else
        {
            policy.Update(command.IsEnabled!.Value, targets, command.ActorUserId, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await SlaPolicyMapper.ToDetailsAsync(policy, configuration, cancellationToken);
    }
}

public sealed class UpdateSlaPolicyValidator : AbstractValidator<UpdateSlaPolicyCommand>
{
    public UpdateSlaPolicyValidator()
    {
        RuleFor(c => c.IsEnabled).NotNull();
        RuleFor(c => c.Targets).NotNull();
        RuleFor(c => c.Targets!)
            .Must(t => t.Count == Enum.GetValues<TicketPriority>().Length)
            .WithMessage("Provide exactly one target per priority.")
            .Must(t => t.Select(x => x.Priority).Distinct().Count() == t.Count)
            .WithMessage("Provide exactly one target per priority.")
            .When(c => c.Targets is not null);
        RuleForEach(c => c.Targets).ChildRules(target =>
        {
            target.RuleFor(t => t.Priority).NotNull().IsInEnum();
            target.RuleFor(t => t.FirstResponseMinutes).InclusiveBetween(1, SlaRules.MaxTargetMinutes);
            target.RuleFor(t => t.ResolutionMinutes).InclusiveBetween(1, SlaRules.MaxTargetMinutes);
            target.RuleFor(t => t.ResolutionMinutes)
                .GreaterThanOrEqualTo(t => t.FirstResponseMinutes)
                .WithMessage("The resolution target cannot be shorter than the first-response target.");
        });
    }
}
