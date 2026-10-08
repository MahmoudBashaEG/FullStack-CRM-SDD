using System.Text.Json;
using Crm.Domain.Tickets;
namespace Crm.Infrastructure.Tickets;

internal static class TicketHistoryValues
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    internal static object? Wire(TicketChangeValue? value) => value is TicketTextValue text ? text.Value : value;
    internal static string? Serialize(TicketChangeValue? value)
    {
        var wire = Wire(value); return wire is null ? null : JsonSerializer.Serialize(wire, wire.GetType(), Options);
    }
    internal static TicketChangeValue? Deserialize(string? value)
    {
        if (value is null) return null;
        using var json = JsonDocument.Parse(value);
        return json.RootElement.ValueKind == JsonValueKind.String ? new TicketTextValue(json.RootElement.GetString()!)
            : json.RootElement.TryGetProperty("reference", out _) ? JsonSerializer.Deserialize<TicketCreationValue>(value, Options)
            : JsonSerializer.Deserialize<TicketReferenceValue>(value, Options);
    }
}
