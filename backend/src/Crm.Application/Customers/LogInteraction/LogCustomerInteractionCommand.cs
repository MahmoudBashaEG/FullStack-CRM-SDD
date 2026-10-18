using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Domain.Customers;
using FluentValidation;
using FluentValidation.Results;
using Mediator;

namespace Crm.Application.Customers.LogInteraction;

/// <summary>Single save. OccurredAtUtc null ⇒ now. Type is nullable only so a missing JSON field fails validation.</summary>
public sealed record LogCustomerInteractionCommand(
    Guid CustomerId, Guid ActorUserId, CustomerInteractionType? Type, string Summary, DateTimeOffset? OccurredAtUtc)
    : ICommand<CustomerInteractionDetails>;

public sealed class LogCustomerInteractionCommandHandler(
    ICustomerRecordRepository records,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<LogCustomerInteractionCommand, CustomerInteractionDetails>
{
    public async ValueTask<CustomerInteractionDetails> Handle(LogCustomerInteractionCommand command, CancellationToken cancellationToken)
    {
        if (!await records.CustomerExistsAsync(command.CustomerId, cancellationToken))
        {
            throw new NotFoundException($"Customer '{command.CustomerId}' was not found.");
        }

        var now = timeProvider.GetUtcNow();
        var occurredAt = (command.OccurredAtUtc ?? now).ToUniversalTime();
        if (occurredAt > now + CustomerRecordRules.InteractionFutureTolerance)
        {
            throw Invalid(nameof(command.OccurredAtUtc), "The interaction time cannot be in the future.");
        }

        var interaction = CustomerInteraction.Record(
            command.CustomerId, command.Type!.Value, command.Summary.Trim(), occurredAt, command.ActorUserId, now);
        records.AddInteraction(interaction);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await records.GetInteractionAsync(interaction.Id, cancellationToken);
    }

    // 400 ValidationProblemDetails keyed by the camelCased field (GlobalExceptionHandler).
    private static ValidationException Invalid(string property, string message) =>
        new([new ValidationFailure(property, message)]);
}

public sealed class LogCustomerInteractionValidator : AbstractValidator<LogCustomerInteractionCommand>
{
    public LogCustomerInteractionValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.Type).NotNull().IsInEnum().WithMessage("Choose Phone Call, Meeting or Other.");
        RuleFor(c => c.Summary).NotEmpty().WithMessage("Enter a summary.").MaximumLength(CustomerRecordRules.InteractionSummaryMaxLength);
        RuleFor(c => c.OccurredAtUtc)
            .GreaterThanOrEqualTo(CustomerRecordRules.InteractionEarliest).WithMessage("Enter a valid interaction time.")
            .When(c => c.OccurredAtUtc is not null);
    }
}
