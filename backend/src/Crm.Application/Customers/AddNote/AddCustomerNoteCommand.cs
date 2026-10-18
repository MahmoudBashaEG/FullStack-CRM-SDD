using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Domain.Customers;
using FluentValidation;
using Mediator;

namespace Crm.Application.Customers.AddNote;

/// <summary>Single save — not transactional. Append-only: there is no edit/delete use case (C2 Option A).</summary>
public sealed record AddCustomerNoteCommand(Guid CustomerId, Guid ActorUserId, string Body) : ICommand<CustomerNoteDetails>;

public sealed class AddCustomerNoteCommandHandler(
    ICustomerRecordRepository records,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<AddCustomerNoteCommand, CustomerNoteDetails>
{
    public async ValueTask<CustomerNoteDetails> Handle(AddCustomerNoteCommand command, CancellationToken cancellationToken)
    {
        if (!await records.CustomerExistsAsync(command.CustomerId, cancellationToken))
        {
            throw new NotFoundException($"Customer '{command.CustomerId}' was not found.");
        }

        var note = CustomerNote.Add(command.CustomerId, command.ActorUserId, command.Body.Trim(), timeProvider.GetUtcNow());
        records.AddNote(note);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await records.GetNoteAsync(note.Id, cancellationToken);
    }
}

public sealed class AddCustomerNoteValidator : AbstractValidator<AddCustomerNoteCommand>
{
    public AddCustomerNoteValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.Body).NotEmpty().WithMessage("Enter a note.").MaximumLength(CustomerRecordRules.NoteMaxLength);
    }
}
