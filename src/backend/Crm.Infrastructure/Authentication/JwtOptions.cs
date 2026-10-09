using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
namespace Crm.Infrastructure.Authentication;

public sealed record JwtOptions(string Issuer, string Audience, string SigningCertificateThumbprint, string SigningKeyId, StoreLocation StoreLocation, string[] PreviousThumbprints)
{
    public static JwtOptions Load(IConfiguration configuration)
    {
        var section = configuration.GetSection("Jwt");
        static string Required(IConfiguration config, string key) => !string.IsNullOrWhiteSpace(config[key]) ? config[key]! : throw new InvalidOperationException("Required JWT configuration is missing.");
        var location = section["CertificateStoreLocation"] ?? "CurrentUser";
        if (!Enum.TryParse<StoreLocation>(location, false, out var store) || !Enum.IsDefined(store)) throw new InvalidOperationException("JWT certificate store is invalid.");
        return new(Required(section,"Issuer"), Required(section,"Audience"), Required(section,"SigningCertificateThumbprint"), Required(section,"SigningKeyId"), store, section.GetSection("PreviousValidationCertificateThumbprints").Get<string[]>() ?? []);
    }
}
