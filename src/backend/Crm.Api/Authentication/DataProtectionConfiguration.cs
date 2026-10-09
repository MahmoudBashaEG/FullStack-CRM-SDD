using Microsoft.AspNetCore.DataProtection;
namespace Crm.Api.Authentication;

public static class DataProtectionConfiguration
{
    public static IServiceCollection AddCrmDataProtection(this IServiceCollection services,IConfiguration configuration)
    {
        if(!OperatingSystem.IsWindows())throw new InvalidOperationException("The supported installation requires Windows key protection.");
        var protection=services.AddDataProtection().SetApplicationName("Crm").ProtectKeysWithDpapi();
        // Framework user-profile repository is durable by default. Owned test/operational
        // repositories still use the same Windows protection, never plaintext keys.
        var directory=configuration["DataProtection:KeyDirectory"];
        if(string.IsNullOrWhiteSpace(directory))
        {
            var profile=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if(string.IsNullOrWhiteSpace(profile))throw new InvalidOperationException("A durable Windows user profile is required.");
            directory=Path.Combine(profile,"ASP.NET","DataProtection-Keys");
        }
        protection.PersistKeysToFileSystem(new DirectoryInfo(Path.GetFullPath(directory)));
        return services;
    }
}
