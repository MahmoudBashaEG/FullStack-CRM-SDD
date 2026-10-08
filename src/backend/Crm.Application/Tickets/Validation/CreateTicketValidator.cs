using Crm.Application.Common;
using Crm.Application.Tickets.Commands;
using Crm.Domain.Tickets;
namespace Crm.Application.Tickets.Validation;

public static class CreateTicketValidator
{
    public static Failure? Validate(CreateTicketCommand command)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(command.Subject)) errors["subject"] = ["Subject is required."];
        if (string.IsNullOrWhiteSpace(command.Description)) errors["description"] = ["Description is required."];
        if (command.Priority is not null && !Enum.GetNames<TicketPriority>().Contains(command.Priority, StringComparer.Ordinal)) errors["priority"] = ["Select a valid priority."];
        return errors.Count == 0 ? null : new("validation_failed", 400, errors);
    }
}
