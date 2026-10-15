using Crm.Application.Abstractions.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Infrastructure.Identity;

internal sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JsonWebTokenHandler handler = new();

    public TimeSpan RefreshTokenLifetime => options.Value.RefreshTokenLifetime;

    public TimeSpan RefreshTokenReuseGracePeriod => options.Value.RefreshTokenReuseGracePeriod;

    public AccessToken CreateAccessToken(UserAccount user, DateTimeOffset now)
    {
        var jwt = options.Value;
        var expiresAtUtc = now.Add(jwt.AccessTokenLifetime);
        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAtUtc.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [CrmClaimTypes.Subject] = user.Id.ToString(),
                [CrmClaimTypes.Role] = user.Role,
                [CrmClaimTypes.SecurityStamp] = user.SecurityStamp,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            },
            SigningCredentials = new SigningCredentials(jwt.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
        });
        return new AccessToken(token, expiresAtUtc);
    }
}
