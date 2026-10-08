using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Api.Authorization;

public static class AuthenticationConfiguration
{
    public static IServiceCollection AddQueueAuthentication(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var settings = configuration.GetSection("Authentication");
        if (!environment.IsEnvironment("Testing") && settings.GetChildren().Any(x => x.Key.StartsWith("Test", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Test authentication configuration is forbidden outside Testing.");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            if (!environment.IsEnvironment("Testing") && settings.GetChildren().Any(x => x.Key.StartsWith("Test", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Test authentication configuration is forbidden outside Testing.");
            options.MapInboundClaims = false;
            options.IncludeErrorDetails = false;
            options.RequireHttpsMetadata = true;
            if (environment.IsEnvironment("Testing"))
            {
                using var rsa = RSA.Create();
                rsa.ImportFromPem(File.ReadAllText(settings["TestPublicKeyPath"] ?? throw new InvalidOperationException("Testing public key is required.")));
                options.TokenValidationParameters = new()
                {
                    ValidateIssuer = true, ValidIssuer = Required(settings, "TestIssuer"),
                    ValidateAudience = true, ValidAudience = Required(settings, "TestAudience"),
                    ValidateIssuerSigningKey = true, IssuerSigningKey = new RsaSecurityKey(rsa.ExportParameters(false)),
                    ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256], ClockSkew = TimeSpan.Zero
                };
            }
            else
            {
                var authority = Required(settings, "Authority");
                if (!Uri.TryCreate(authority, UriKind.Absolute, out var uri) || uri.Scheme != "https") throw new InvalidOperationException("HTTPS authentication authority is required.");
                options.Authority = authority;
                options.Audience = Required(settings, "Audience");
                options.TokenValidationParameters.ValidateIssuer = true;
                options.TokenValidationParameters.ValidateAudience = true;
                options.TokenValidationParameters.ValidateLifetime = true;
                options.TokenValidationParameters.ValidateIssuerSigningKey = true;
                options.TokenValidationParameters.RequireSignedTokens = true;
            }
        });
        services.AddAuthorization(SupportQueuePolicy.Configure);
        return services;
    }
    private static string Required(IConfiguration settings, string name) => !string.IsNullOrWhiteSpace(settings[name]) ? settings[name]! : throw new InvalidOperationException("Required authentication configuration is missing.");
}
