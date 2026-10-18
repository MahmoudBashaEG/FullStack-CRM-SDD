using Crm.Application.Abstractions.Persistence;
using Crm.Application.Administration.GetOrganizationSettings;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Administration;
using FluentValidation;
using Mediator;

namespace Crm.Application.Administration.RemoveOrganizationLogo;

/// <summary>No logo ⇒ nothing is saved (no audit, same version). Otherwise one save: settings UPDATE + logo DELETE + audit INSERT.</summary>
public sealed record RemoveOrganizationLogoCommand(Guid ActorUserId, byte[] ExpectedVersion) : ICommand<OrganizationSettingsDetails>;

public sealed class RemoveOrganizationLogoCommandHandler(
    IOrganizationSettingsRepository settings,
    IAdminAuditRepository audit,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<RemoveOrganizationLogoCommand, OrganizationSettingsDetails>
{
    public async ValueTask<OrganizationSettingsDetails> Handle(RemoveOrganizationLogoCommand command, CancellationToken cancellationToken)
    {
        var current = await settings.RequireAsync(cancellationToken);
        if (!settings.GetVersion(current).AsSpan().SequenceEqual(command.ExpectedVersion))
        {
            throw new ConflictException("The organization settings were changed by someone else. Reload them and try again.");
        }

        var now = timeProvider.GetUtcNow();
        var changes = current.RemoveLogo(command.ActorUserId, now);
        if (changes.Count > 0)
        {
            if (await settings.FindLogoAsync(cancellationToken) is { } logo)
            {
                settings.RemoveLogo(logo);
            }

            audit.Add(AdminAuditEntry.OrganizationSettingsUpdated(command.ActorUserId, changes, now));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await OrganizationSettingsMapper.ToDetailsAsync(current, settings, cancellationToken);
    }
}

public sealed class RemoveOrganizationLogoValidator : AbstractValidator<RemoveOrganizationLogoCommand>
{
    public RemoveOrganizationLogoValidator() => RuleFor(c => c.ExpectedVersion).NotEmpty();
}
