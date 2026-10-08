using System.Security.Claims;
using Crm.Application.Common;

namespace Crm.Api.Authorization;

public sealed class CurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    public Actor Get()
    {
        var user = accessor.HttpContext?.User ?? throw new InvalidOperationException("A trusted actor is required.");
        if (!SupportQueuePolicy.HasAccess(user)) throw new InvalidOperationException("Queue authorization is required.");
        var subject = user.FindFirstValue("sub")!;
        var displayName = user.FindFirstValue("name");
        return new(user.FindFirstValue("iss")!, subject, string.IsNullOrWhiteSpace(displayName) ? subject : displayName);
    }
}
