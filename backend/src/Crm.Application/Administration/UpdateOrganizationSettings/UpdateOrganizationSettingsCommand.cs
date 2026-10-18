using Crm.Application.Abstractions.Persistence;
using Crm.Application.Administration.GetOrganizationSettings;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Domain.Administration;
using FluentValidation;
using Mediator;

namespace Crm.Application.Administration.UpdateOrganizationSettings;

/// <summary>Single save (settings + audit) — atomic without ITransactionalCommand. Requires the current version (If-Match).
/// AccentColor null (omitted by an older client) keeps the saved accent; a preset id sets it (PLAT-001 C1).</summary>
public sealed record UpdateOrganizationSettingsCommand(
    Guid ActorUserId, byte[] ExpectedVersion, string? DisplayName, string? SupportEmail, string? SupportPhone, string? SupportInformation,
    string? AccentColor = null)
    : ICommand<OrganizationSettingsDetails>;

public sealed class UpdateOrganizationSettingsCommandHandler(
    IOrganizationSettingsRepository settings,
    IAdminAuditRepository audit,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<UpdateOrganizationSettingsCommand, OrganizationSettingsDetails>
{
    public async ValueTask<OrganizationSettingsDetails> Handle(UpdateOrganizationSettingsCommand command, CancellationToken cancellationToken)
    {
        var current = await settings.RequireAsync(cancellationToken);
        if (!settings.GetVersion(current).AsSpan().SequenceEqual(command.ExpectedVersion))
        {
            throw new ConflictException("The organization settings were changed by someone else. Reload them and try again.");
        }

        var now = timeProvider.GetUtcNow();
        var changes = current.Update(
            command.DisplayName!.Trim(),
            AdministrationRules.Normalize(command.SupportEmail),
            AdministrationRules.Normalize(command.SupportPhone),
            AdministrationRules.Normalize(command.SupportInformation),
            AdministrationRules.Normalize(command.AccentColor) is { } accent ? BrandingRules.ToStoredAccent(accent) : current.AccentColor,
            command.ActorUserId,
            now);

        if (changes.Count > 0)
        {
            audit.Add(AdminAuditEntry.OrganizationSettingsUpdated(command.ActorUserId, changes, now));
            await unitOfWork.SaveChangesAsync(cancellationToken); // one statement batch: UPDATE (rowversion-checked) + INSERT audit
        }

        return await OrganizationSettingsMapper.ToDetailsAsync(current, settings, cancellationToken);
    }
}

public sealed class UpdateOrganizationSettingsValidator : AbstractValidator<UpdateOrganizationSettingsCommand>
{
    public UpdateOrganizationSettingsValidator()
    {
        RuleFor(c => c.ExpectedVersion).NotEmpty();
        RuleFor(c => c.DisplayName).NotEmpty().Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Enter the organization name.")
            .MaximumLength(AdministrationRules.OrganizationNameMaxLength);
        RuleFor(c => c.SupportEmail).EmailAddress().MaximumLength(AdministrationRules.SupportEmailMaxLength)
            .When(c => !string.IsNullOrWhiteSpace(c.SupportEmail));
        RuleFor(c => c.SupportPhone).MaximumLength(AdministrationRules.SupportPhoneMaxLength);
        RuleFor(c => c.SupportInformation).MaximumLength(AdministrationRules.SupportInformationMaxLength);
        RuleFor(c => c.AccentColor).Must(v => BrandingRules.IsAccentPreset(AdministrationRules.Normalize(v)))
            .WithMessage("Choose one of the available accent colors.");
    }
}
