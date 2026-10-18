using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Domain.Customers;
using FluentValidation;
using Mediator;

namespace Crm.Application.Customers.UploadAttachment;

/// <summary>
/// Single save. FileName is the client's original name (sanitized here); Content is the whole file (≤ 5 MB, checked by the validator).
/// No client content type: the stored type comes from the extension allow-list.
/// </summary>
public sealed record UploadCustomerAttachmentCommand(Guid CustomerId, Guid ActorUserId, string FileName, byte[] Content)
    : ICommand<CustomerAttachmentDetails>;

public sealed class UploadCustomerAttachmentCommandHandler(
    ICustomerRecordRepository records,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<UploadCustomerAttachmentCommand, CustomerAttachmentDetails>
{
    public async ValueTask<CustomerAttachmentDetails> Handle(UploadCustomerAttachmentCommand command, CancellationToken cancellationToken)
    {
        if (!await records.CustomerExistsAsync(command.CustomerId, cancellationToken))
        {
            throw new NotFoundException($"Customer '{command.CustomerId}' was not found.");
        }

        var fileName = CustomerAttachmentRules.SanitizeFileName(command.FileName);
        var attachment = CustomerAttachment.Upload(
            Guid.CreateVersion7(),
            command.CustomerId,
            fileName,
            CustomerAttachmentRules.ContentTypeFor(fileName),
            command.Content,
            command.ActorUserId,
            timeProvider.GetUtcNow());

        records.AddAttachment(attachment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await records.GetAttachmentAsync(attachment.Id, cancellationToken);
    }
}

public sealed class UploadCustomerAttachmentValidator : AbstractValidator<UploadCustomerAttachmentCommand>
{
    public UploadCustomerAttachmentValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        // One key ("file") so Angular shows one message next to the picker.
        RuleFor(c => c).Custom((c, context) =>
        {
            if (CustomerAttachmentRules.Check(c.FileName, c.Content) is { } problem)
            {
                context.AddFailure("file", problem);
            }
        });
    }
}
