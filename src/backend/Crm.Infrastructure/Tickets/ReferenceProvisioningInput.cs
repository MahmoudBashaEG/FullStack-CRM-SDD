using System.Text.Json;
using System.Text.Json.Serialization;
using Crm.Application.Tickets.Contracts;
namespace Crm.Infrastructure.Tickets;

public sealed record CategoryInput([property: JsonRequired] Guid Id, [property: JsonRequired] string DisplayName, [property: JsonRequired] bool IsAvailable);
public sealed record AgentInput([property: JsonRequired] Guid Id, [property: JsonRequired] string Issuer, [property: JsonRequired] string Subject, [property: JsonRequired] string DisplayName, [property: JsonRequired] bool IsEligible);
public sealed class ReferenceProvisioningInput
{
    public required int SchemaVersion { get; init; }
    public CategoryInput[]? Categories { get; init; }
    public AgentInput[]? Agents { get; init; }
    public static ReferenceProvisioningInput Parse(string text)
    {
        using var document = JsonDocument.Parse(text);
        RejectDuplicates(document.RootElement);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || new[] { "categories", "agents" }.Any(name => root.TryGetProperty(name, out var item) && item.ValueKind != JsonValueKind.Array)) throw new JsonException();
        var input = JsonSerializer.Deserialize<ReferenceProvisioningInput>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow }) ?? throw new JsonException();
        var categories = input.Categories ?? [];
        var agents = input.Agents ?? [];
        if (agents.Length > 0) throw new JsonException();
        if (input.SchemaVersion != 1 || categories.Length + agents.Length == 0 || categories.Any(x => x is null || x.Id == Guid.Empty || string.IsNullOrWhiteSpace(x.DisplayName)) ||
            agents.Any(x => x is null || x.Id == Guid.Empty || string.IsNullOrWhiteSpace(x.DisplayName) || string.IsNullOrWhiteSpace(x.Subject) || !Uri.TryCreate(x.Issuer, UriKind.Absolute, out var uri) || uri.Scheme != "https") ||
            categories.Select(x => x.Id).Distinct().Count() != categories.Length || agents.Select(x => x.Id).Distinct().Count() != agents.Length ||
            agents.Select(x => (x.Issuer, x.Subject)).Distinct().Count() != agents.Length ||
            categories.Any(x => x.Id == TicketDefaults.CategoryId && (!x.IsAvailable || x.DisplayName.Trim() != "Uncategorised"))) throw new JsonException();
        return input;
    }
    private static void RejectDuplicates(JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in item.EnumerateObject()) { if (!names.Add(property.Name)) throw new JsonException(); RejectDuplicates(property.Value); }
        }
        else if (item.ValueKind == JsonValueKind.Array) foreach (var child in item.EnumerateArray()) RejectDuplicates(child);
    }
}
