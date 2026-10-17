using System.Security.Claims;
using System.Threading.RateLimiting;
using Crm.Application.Abstractions.Identity;
using Crm.Domain.Identity;
using Crm.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Api.Authentication;

internal static class SecurityRegistration
{
    public static IServiceCollection AddSecurity(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false; // keep "sub" / "role" as issued
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwt.CreateSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = CrmClaimTypes.Subject,
                    RoleClaimType = CrmClaimTypes.Role,
                };
                bearer.Events = new JwtBearerEvents
                {
                    // Server-side account-state check: disable / password change / reset revoke JWTs immediately.
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal!;
                        var stamp = principal.FindFirstValue(CrmClaimTypes.SecurityStamp);
                        if (!Guid.TryParse(principal.FindFirstValue(CrmClaimTypes.Subject), out var userId) || stamp is null)
                        {
                            context.Fail("Invalid token claims.");
                            return;
                        }

                        var identity = context.HttpContext.RequestServices.GetRequiredService<IIdentityService>();
                        var user = await identity.FindByIdAsync(userId, context.HttpContext.RequestAborted);
                        if (user is null || !user.IsActive || !string.Equals(user.SecurityStamp, stamp, StringComparison.Ordinal))
                        {
                            context.Fail("Account state changed.");
                        }
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()) // auth by default
            .AddPolicy(AuthorizationPolicies.SuperAdmin, policy => policy.RequireRole(UserRoles.SuperAdmin))
            .AddPolicy(AuthorizationPolicies.Staff, policy => policy.RequireRole(UserRoles.SuperAdmin, UserRoles.Agent))
            .AddPolicy(AuthorizationPolicies.Customer, policy => policy.RequireRole(UserRoles.Customer));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy<string>(RateLimitPolicies.Credentials, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

        return services;
    }
}
