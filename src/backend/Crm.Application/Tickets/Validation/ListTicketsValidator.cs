using Crm.Application.Common;
using Crm.Application.Tickets.Queries;
using Crm.Domain.Tickets;
namespace Crm.Application.Tickets.Validation;

public static class ListTicketsValidator
{
    public static OperationResult<TicketListCriteria> Validate(ListTicketsQuery query)
    {
        var errors = new Dictionary<string, string[]>();
        var status = ParseEnum<TicketStatus>(query.Status, "status", errors);
        var priority = ParseEnum<TicketPriority>(query.Priority, "priority", errors);
        var category = ParseGuid(query.CategoryId, "categoryId", errors);
        var unassigned = query.Assignment == "unassigned";
        var agent = unassigned ? null : ParseGuid(query.Assignment, "assignment", errors);
        if (query.Page < 1) errors["page"] = ["Page must be at least 1."];
        if (query.PageSize is < 1 or > 100) errors["pageSize"] = ["Page size must be between 1 and 100."];
        if (errors.Count > 0) return OperationResult<TicketListCriteria>.Fail("validation_failed", 400, errors);
        var term = query.Q?.Trim();
        return OperationResult<TicketListCriteria>.Success(new(string.IsNullOrEmpty(term) ? null : term, status, category, priority, unassigned, agent, query.Page, query.PageSize));
    }
    private static T? ParseEnum<T>(string? value, string field, Dictionary<string, string[]> errors) where T : struct, Enum
    {
        if (value is null) return null;
        if (Enum.GetNames<T>().Contains(value, StringComparer.Ordinal)) return Enum.Parse<T>(value);
        errors[field] = ["Select one valid value."]; return null;
    }
    private static Guid? ParseGuid(string? value, string field, Dictionary<string, string[]> errors)
    {
        if (value is null) return null;
        if (Guid.TryParse(value, out var id)) return id;
        errors[field] = ["Provide one valid identifier."]; return null;
    }
}
