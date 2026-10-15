using System.Security.Claims;
using Crm.Application.Common.Exceptions;
using Crm.Infrastructure.Identity;

namespace Crm.Api.Authentication;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(CrmClaimTypes.Subject), out var id)
            ? id
            : throw new UnauthorizedException("The access token has no valid subject.");
}
