using System.Globalization;
using Crm.Application.Authentication.Contracts;
using Crm.Application.Authentication.Ports;
using Crm.Application.Common;
using Crm.Domain.Accounts;
using Crm.Infrastructure.Authentication;
using Crm.Api.Errors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
namespace Crm.Api.Authorization;

public static class AuthenticationConfiguration
{
    public static IServiceCollection AddQueueAuthentication(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        if (configuration.GetSection("Authentication").GetChildren().Any(x => x.Key.StartsWith("Test", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Legacy test authentication configuration is forbidden.");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<JwtOptions, SigningCertificateProvider>((options, settings, keys) =>
        {
            options.MapInboundClaims = false; options.IncludeErrorDetails = false;
            options.TokenValidationParameters = new()
            {
                ValidateIssuer = true, ValidIssuer = settings.Issuer, ValidateAudience = true, ValidAudience = settings.Audience,
                ValidateIssuerSigningKey = true, IssuerSigningKeys = keys.ValidationKeys, ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256], ValidTypes = ["JWT"], ClockSkew = TimeSpan.Zero,
                RoleClaimType = "role", NameClaimType = "sub", IncludeTokenOnFailedValidation = false
            };
            options.Events = new()
            {
                OnTokenValidated = async context =>
                {
                    var principal = context.Principal!;
                    string? Single(string type) { var values = principal.FindAll(type).ToArray(); return values.Length == 1 ? values[0].Value : null; }
                    if (!Guid.TryParseExact(Single("sub"), "D", out var userId) || !Guid.TryParseExact(Single("sid"), "D", out var sessionId) ||
                        !Enum.TryParse<AccountRole>(Single("role"), false, out var role) || !Enum.IsDefined(role) || Single("role") != role.ToString() ||
                        !long.TryParse(Single("av"), NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version < 1 || !Guid.TryParseExact(Single("jti"), "D", out _) ||
                        !long.TryParse(Single("iat"), NumberStyles.None,CultureInfo.InvariantCulture,out var issued) ||
                        !long.TryParse(Single("nbf"), NumberStyles.None,CultureInfo.InvariantCulture,out var starts) ||
                        !long.TryParse(Single("exp"), NumberStyles.None,CultureInfo.InvariantCulture,out var expires) || starts != issued || expires <= issued || expires-issued > 300)
                    { context.Fail("Invalid authentication."); return; }
                    var reader = context.HttpContext.RequestServices.GetRequiredService<ICurrentAuthenticationReader>();
                    TrustedAuthentication? authentication;
                    try { authentication = await reader.ValidateAsync(new(userId, sessionId, role, version),context.HttpContext.RequestAborted); }
                    catch (DependencyUnavailableException error) { context.Fail(error); return; }
                    if (authentication is null) { context.Fail("Invalid authentication."); return; }
                    context.HttpContext.Items[typeof(TrustedAuthentication)] = authentication;
                },
                OnAuthenticationFailed = context =>
                {
                    if (context.Exception is DependencyUnavailableException) throw context.Exception;
                    return Task.CompletedTask;
                },
                OnChallenge = async context =>
                {
                    context.HandleResponse(); context.Response.Headers.WWWAuthenticate = "Bearer";
                    if (context.AuthenticateFailure is DependencyUnavailableException)
                    {
                        context.Response.Headers.Remove("WWW-Authenticate");
                        await ProblemDetailsMapping.WriteAsync(context.HttpContext,new("service_unavailable",503)); return;
                    }
                    await ProblemDetailsMapping.WriteAsync(context.HttpContext,new(context.AuthenticateFailure is SecurityTokenExpiredException ? "token_expired" : "authentication_invalid",401));
                },
                OnForbidden = context => ProblemDetailsMapping.WriteAsync(context.HttpContext,new("forbidden",403))
            };
        });
        services.AddAuthorization(CrmPolicies.Configure);
        return services;
    }
}
