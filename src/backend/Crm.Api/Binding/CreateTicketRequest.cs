using System.Text.Json;
using Crm.Application.Tickets.Commands;
namespace Crm.Api.Binding;

public sealed record CreateTicketRequest(string? Subject, string? Description, JsonElement CategoryId, JsonElement Priority)
{
    public bool TryBind(out CreateTicketCommand command)
    {
        Guid? category = null; string? priority = null;
        command = new(Subject, Description, null, null);
        if (CategoryId.ValueKind != JsonValueKind.Undefined)
        {
            if (CategoryId.ValueKind != JsonValueKind.String || !CategoryId.TryGetGuid(out var id)) return false;
            category = id;
        }
        if (Priority.ValueKind != JsonValueKind.Undefined)
        {
            if (Priority.ValueKind != JsonValueKind.String) return false;
            priority = Priority.GetString();
        }
        command = new(Subject, Description, category, priority); return true;
    }
}
