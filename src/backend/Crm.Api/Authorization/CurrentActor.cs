using Crm.Application.Common;
using Crm.Application.Authentication.Contracts;

namespace Crm.Api.Authorization;

public sealed class CurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    public Actor Get()
    {
        var trusted=accessor.HttpContext?.Items[typeof(TrustedAuthentication)] as TrustedAuthentication
            ?? throw new InvalidOperationException("A trusted actor is required.");
        return new("urn:crm:local:user",trusted.Identity.UserId.ToString("D"),trusted.Identity.DisplayName);
    }
}
