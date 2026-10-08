using System.Text.Json;
using Crm.Application.Common;
using Crm.Application.Tickets.Commands;
using Crm.Application.Tickets.Validation;
namespace Crm.Api.Binding;

public sealed record TicketStatusRequest(JsonElement ExpectedVersion, JsonElement Status)
{
    public Failure? Bind(string reference, out ChangeTicketStatusCommand command)
    {
        var version = ExpectedVersion.ValueKind == JsonValueKind.String ? ExpectedVersion.GetString() : null;
        command = new(reference, version, null);
        var failure = TicketMutationValidators.Version(version, out _); if (failure is not null) return failure;
        if (Status.ValueKind != JsonValueKind.String) return new("validation_failed", 400, new() { ["status"] = ["Supply a status string."] });
        command = new(reference, version, Status.GetString()); return null;
    }
}
