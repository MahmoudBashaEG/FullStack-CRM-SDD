using Crm.Application.Abstractions.Persistence;
using Crm.Application.Administration.GetOrganizationSettings;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Domain.Administration;
using FluentValidation;
using Mediator;

namespace Crm.Application.Administration.SetOrganizationLogo;

/// <summary>Single save: settings metadata UPDATE (rowversion-checked) + logo INSERT/UPDATE + audit INSERT (PLAT-001 D9).</summary>
public sealed record SetOrganizationLogoCommand(Guid ActorUserId, byte[] ExpectedVersion, string FileName, byte[] Content)
    : ICommand<OrganizationSettingsDetails>;

public sealed class SetOrganizationLogoCommandHandler(
    IOrganizationSettingsRepository settings,
    IAdminAuditRepository audit,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SetOrganizationLogoCommand, OrganizationSettingsDetails>
{
    public async ValueTask<OrganizationSettingsDetails> Handle(SetOrganizationLogoCommand command, CancellationToken cancellationToken)
    {
        var current = await settings.RequireAsync(cancellationToken);
        if (!settings.GetVersion(current).AsSpan().SequenceEqual(command.ExpectedVersion))
        {
            throw new ConflictException("The organization settings were changed by someone else. Reload them and try again.");
        }

        var contentType = BrandingRules.DetectImageType(command.Content)!; // the validator guarantees a supported signature
        var now = timeProvider.GetUtcNow();
        var changes = current.SetLogo(contentType, command.Content.Length, command.ActorUserId, now);

        var logo = await settings.FindLogoAsync(cancellationToken);
        if (logo is null)
        {
            settings.AddLogo(OrganizationLogo.Create(command.Content, contentType));
        }
        else
        {
            logo.Replace(command.Content, contentType);
        }

        audit.Add(AdminAuditEntry.OrganizationSettingsUpdated(command.ActorUserId, changes, now));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await OrganizationSettingsMapper.ToDetailsAsync(current, settings, cancellationToken);
    }
}

public sealed class SetOrganizationLogoValidator : AbstractValidator<SetOrganizationLogoCommand>
{
    public SetOrganizationLogoValidator()
    {
        RuleFor(c => c.ExpectedVersion).NotEmpty();
        RuleFor(c => c).Custom((c, context) =>
        {
            if (BrandingRules.CheckLogo(c.FileName, c.Content) is { } problem)
            {
                context.AddFailure("file", problem);
            }
        });
    }
}
