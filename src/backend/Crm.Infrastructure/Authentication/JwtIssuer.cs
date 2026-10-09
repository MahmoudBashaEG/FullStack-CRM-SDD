using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Crm.Application.Authentication.Contracts;
using Crm.Application.Authentication.Ports;
namespace Crm.Infrastructure.Authentication;

public sealed class JwtIssuer(JwtOptions options, SigningCertificateProvider keys, TimeProvider clock) : IAccessTokenIssuer
{
    public AccessTokenEnvelope Issue(TrustedAuthentication authentication, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTimeOffset.FromUnixTimeSeconds(clock.GetUtcNow().ToUnixTimeSeconds());
        var maximum = now.AddMinutes(5);
        var expires = DateTimeOffset.FromUnixTimeSeconds((authentication.ExpiresAtUtc < maximum ? authentication.ExpiresAtUtc : maximum).ToUnixTimeSeconds());
        if (authentication.Identity.UserId == Guid.Empty || authentication.SessionId == Guid.Empty || authentication.AuthenticationVersion < 1 || !Enum.IsDefined(authentication.Identity.Role) || expires <= now)
            throw new InvalidOperationException("A live authenticated session is required.");
        var claims = new[] { new Claim("sub", authentication.Identity.UserId.ToString("D")), new Claim("role",authentication.Identity.Role.ToString()), new Claim("sid",authentication.SessionId.ToString("D")),
            new Claim("av",authentication.AuthenticationVersion.ToString(CultureInfo.InvariantCulture)),new Claim("jti",Guid.NewGuid().ToString("D")) };
        var payload = new JwtPayload(options.Issuer, options.Audience, claims, now.UtcDateTime, expires.UtcDateTime, now.UtcDateTime);
        var jwt = new JwtSecurityToken(new JwtHeader(keys.Credentials), payload);
        return new(new JwtSecurityTokenHandler().WriteToken(jwt), "Bearer", expires);
    }
}
