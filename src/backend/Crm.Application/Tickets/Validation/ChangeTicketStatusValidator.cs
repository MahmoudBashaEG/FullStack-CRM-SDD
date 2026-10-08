using Crm.Application.Common;
using Crm.Domain.Tickets;
namespace Crm.Application.Tickets.Validation;

public static class ChangeTicketStatusValidator
{
    public static Failure? Validate(string? value, out TicketStatus status)
    {
        if (value is not null && Enum.TryParse(value, out status) && Enum.IsDefined(status) && status.ToString() == value) return null;
        status = default;
        return new("validation_failed", 400, new() { ["status"] = ["Choose Open, InProgress, Resolved or Closed."] });
    }
}
