using Crm.Application.Common;
using Crm.Application.Tickets.Commands;
using Crm.Application.Tickets.Contracts;
using Crm.Domain.Tickets;
namespace Crm.Application.Tickets.Validation;

public static class TicketMutationValidators
{
    public static Failure? Version(string? version, out byte[] bytes) => EditVersion.TryParse(version, out bytes) ? null : new("invalid_version", 400, new() { ["expectedVersion"] = ["A canonical eight-byte edit version is required."] });
    public static Failure? Details(DetailPatch patch)
    {
        var errors = new Dictionary<string, string[]>();
        if (patch.Subject is null && patch.Description is null && patch.CategoryId is null && patch.Priority is null) errors["details"] = ["Supply at least one editable field."];
        if (patch.Subject is not null && string.IsNullOrWhiteSpace(patch.Subject)) errors["subject"] = ["Subject is required."];
        if (patch.Description is not null && string.IsNullOrWhiteSpace(patch.Description)) errors["description"] = ["Description is required."];
        if (patch.CategoryId == Guid.Empty) errors["categoryId"] = ["A category identifier is required."];
        if (patch.Priority is not null && (!Enum.TryParse<TicketPriority>(patch.Priority, out var priority) || !Enum.IsDefined(priority) || priority.ToString() != patch.Priority)) errors["priority"] = ["Choose Low, Normal, High or Urgent."];
        return errors.Count == 0 ? null : new("validation_failed", 400, errors);
    }
}
