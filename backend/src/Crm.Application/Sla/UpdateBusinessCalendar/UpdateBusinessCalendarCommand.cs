using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Validation;
using Crm.Application.Sla.GetBusinessCalendar;
using Crm.Domain.Sla;
using FluentValidation;
using Mediator;

namespace Crm.Application.Sla.UpdateBusinessCalendar;

/// <summary>Creates or replaces the singleton calendar. Single save — not transactional. Applies to tickets created afterwards only.</summary>
public sealed record UpdateBusinessCalendarCommand(
    Guid ActorUserId,
    string TimeZoneId,
    IReadOnlyList<DayOfWeek>? WorkingDays,
    TimeOnly? OpensAt,
    TimeOnly? ClosesAt,
    IReadOnlyList<HolidayInput>? Holidays) : ICommand<BusinessCalendarDetails>;

public sealed record HolidayInput(DateOnly? Date, string? Name);

public sealed class UpdateBusinessCalendarCommandHandler(
    ISlaConfigurationRepository configuration,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<UpdateBusinessCalendarCommand, BusinessCalendarDetails>
{
    public async ValueTask<BusinessCalendarDetails> Handle(UpdateBusinessCalendarCommand command, CancellationToken cancellationToken)
    {
        var timeZoneId = command.TimeZoneId.Trim();
        var holidays = (command.Holidays ?? [])
            .Select(h => new BusinessHoliday(h.Date!.Value, h.Name!.Trim()))
            .OrderBy(h => h.Date)
            .ToList();
        var now = timeProvider.GetUtcNow();

        var calendar = await configuration.FindCalendarAsync(cancellationToken);
        if (calendar is null)
        {
            calendar = BusinessCalendar.Create(
                timeZoneId, command.WorkingDays!, command.OpensAt!.Value, command.ClosesAt!.Value, holidays, command.ActorUserId, now);
            configuration.Add(calendar);
        }
        else
        {
            calendar.Update(timeZoneId, command.WorkingDays!, command.OpensAt!.Value, command.ClosesAt!.Value, holidays, command.ActorUserId, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BusinessCalendarMapper.ToDetailsAsync(calendar, configuration, cancellationToken);
    }
}

public sealed class UpdateBusinessCalendarValidator : AbstractValidator<UpdateBusinessCalendarCommand>
{
    public UpdateBusinessCalendarValidator()
    {
        RuleFor(c => c.TimeZoneId)
            .NotEmpty()
            .MaximumLength(SlaRules.TimeZoneIdMaxLength)
            .Must(id => TimeZoneInfo.TryFindSystemTimeZoneById(id.Trim(), out _))
            .WithMessage("Unknown time zone. Use an IANA id such as Africa/Cairo.");

        RuleFor(c => c.WorkingDays)
            .NotEmpty().WithMessage("Select at least one working day.")
            .Must(days => days!.Distinct().Count() == days!.Count).WithMessage("Working days must be unique.")
            .Must(days => days!.All(Enum.IsDefined)).WithMessage("Unknown working day.")
            .When(c => c.WorkingDays is not null);

        RuleFor(c => c.OpensAt).NotNull();
        RuleFor(c => c.ClosesAt).NotNull();
        RuleFor(c => c)
            .Must(c => c.OpensAt!.Value < c.ClosesAt!.Value)
            .WithName(nameof(UpdateBusinessCalendarCommand.ClosesAt))
            .WithMessage("Closing time must be after opening time.")
            .When(c => c.OpensAt is not null && c.ClosesAt is not null);

        RuleFor(c => c.Holidays)
            .Must(h => h!.Count <= SlaRules.MaxHolidays).WithMessage($"At most {SlaRules.MaxHolidays} holidays.")
            .Must(h => h!.Where(x => x.Date is not null).Select(x => x.Date).Distinct().Count() == h!.Count(x => x.Date is not null))
            .WithMessage("Holiday dates must be unique.")
            .When(c => c.Holidays is not null);
        RuleForEach(c => c.Holidays).ChildRules(holiday =>
        {
            holiday.RuleFor(h => h.Date).NotNull();
            holiday.RuleFor(h => h.Name).NotEmpty().MaximumLength(SlaRules.HolidayNameMaxLength);
        });
    }
}
