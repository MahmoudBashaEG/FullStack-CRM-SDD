using System.Globalization;
using Crm.Application.Tickets.Queries;
namespace Crm.Api.Binding;

public static class TicketListRequest
{
    private static readonly HashSet<string> Keys = new(["q", "status", "categoryId", "priority", "assignment", "page", "pageSize"], StringComparer.Ordinal);
    public static bool TryBind(IQueryCollection values, out ListTicketsQuery query)
    {
        query = new();
        if (values.Any(pair => !Keys.Contains(pair.Key) || pair.Value.Count != 1)) return false;
        var page = 1; var size = 25;
        if (values.TryGetValue("page", out var pageValue) && !int.TryParse(pageValue.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out page)) return false;
        if (values.TryGetValue("pageSize", out var sizeValue) && !int.TryParse(sizeValue.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out size)) return false;
        string? Optional(string key) => values.TryGetValue(key, out var value) ? value.ToString() : null;
        query = new(Optional("q"), Optional("status"), Optional("categoryId"), Optional("priority"), Optional("assignment"), page, size);
        return true;
    }
}
