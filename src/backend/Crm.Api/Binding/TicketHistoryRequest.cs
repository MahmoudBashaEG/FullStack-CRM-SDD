using System.Globalization;
using Crm.Application.Tickets.Queries;
namespace Crm.Api.Binding;

public static class TicketHistoryRequest
{
    public static bool TryBind(string reference, IQueryCollection values, out GetTicketHistoryQuery query)
    {
        query = new(reference); var page = 1; var size = 25;
        if (values.TryGetValue("page", out var pageValue) && !int.TryParse(pageValue.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out page)) return false;
        if (values.TryGetValue("pageSize", out var sizeValue) && !int.TryParse(sizeValue.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out size)) return false;
        query = new(reference, page, size); return true;
    }
}
