using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Domain.Sla;
using FluentValidation;
using Mediator;

namespace Crm.Application.Sla.PreviewDueDate;

/// <summary>Deterministic check of the saved calendar: StartUtc + Minutes of business time. Start defaults to now.</summary>
public sealed record PreviewSlaDueDateQuery(DateTimeOffset? StartUtc, int Minutes) : IQuery<SlaDuePreview>;

public sealed class PreviewSlaDueDateQueryHandler(ISlaConfigurationRepository configuration, TimeProvider timeProvider)
    : IQueryHandler<PreviewSlaDueDateQuery, SlaDuePreview>
{
    public async ValueTask<SlaDuePreview> Handle(PreviewSlaDueDateQuery query, CancellationToken cancellationToken)
    {
        var calendar = await configuration.FindCalendarAsync(cancellationToken)
            ?? throw new ConflictException("Save the business calendar first.");
        var start = (query.StartUtc ?? timeProvider.GetUtcNow()).ToUniversalTime();
        var due = BusinessTimeCalculator.AddBusinessMinutes(start, query.Minutes, calendar.ToRules());
        return new SlaDuePreview(start, query.Minutes, due, calendar.TimeZoneId);
    }
}

public sealed class PreviewSlaDueDateValidator : AbstractValidator<PreviewSlaDueDateQuery>
{
    public PreviewSlaDueDateValidator()
    {
        RuleFor(q => q.Minutes).InclusiveBetween(1, SlaRules.MaxPreviewMinutes);
    }
}
