using System.Text.Json;
using Crm.Application.Common;
using Crm.Application.Tickets.Commands;
using Crm.Application.Tickets.Validation;
namespace Crm.Api.Binding;

public sealed record TicketDetailsRequest(JsonElement ExpectedVersion, JsonElement Subject, JsonElement Description, JsonElement CategoryId, JsonElement Priority)
{
    public Failure? Bind(string reference, out UpdateTicketDetailsCommand command)
    {
        var version = ExpectedVersion.ValueKind == JsonValueKind.String ? ExpectedVersion.GetString() : null;
        command = new(reference, version, new());
        var failure = TicketMutationValidators.Version(version, out _); if (failure is not null) return failure;
        if (!Text(Subject) || !Text(Description) || !Text(Priority) || CategoryId.ValueKind != JsonValueKind.Undefined && (CategoryId.ValueKind != JsonValueKind.String || !CategoryId.TryGetGuid(out _))) return new("validation_failed", 400);
        command = new(reference, version, new(Value(Subject), Value(Description), CategoryId.ValueKind == JsonValueKind.Undefined ? null : CategoryId.GetGuid(), Value(Priority)));
        return null;
    }
    private static bool Text(JsonElement element) => element.ValueKind is JsonValueKind.Undefined or JsonValueKind.String;
    private static string? Value(JsonElement element) => element.ValueKind == JsonValueKind.String ? element.GetString() : null;
}
public sealed record TicketAssignmentRequest(JsonElement ExpectedVersion, JsonElement AgentId)
{
    public Failure? Bind(string reference, out AssignTicketCommand command)
    {
        var version = ExpectedVersion.ValueKind == JsonValueKind.String ? ExpectedVersion.GetString() : null;
        command = new(reference, version, null);
        var failure = TicketMutationValidators.Version(version, out _); if (failure is not null) return failure;
        if (AgentId.ValueKind != JsonValueKind.Null && (AgentId.ValueKind != JsonValueKind.String || !AgentId.TryGetGuid(out _))) return new("validation_failed", 400, new() { ["agentId"] = ["Supply an agent identifier or explicit null."] });
        command = new(reference, version, AgentId.ValueKind == JsonValueKind.Null ? null : AgentId.GetGuid()); return null;
    }
}
