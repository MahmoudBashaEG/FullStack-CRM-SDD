using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
namespace Crm.Infrastructure.Identity;

public static class IdentityConfiguration
{
    public static IServiceCollection AddCrmIdentity(this IServiceCollection services)
    {
        services.AddIdentityCore<LocalUser>(options =>
        {
            options.User.RequireUniqueEmail = true; options.User.AllowedUserNameCharacters = "";
            options.Password.RequiredLength = 15; options.Password.RequireDigit = options.Password.RequireUppercase = options.Password.RequireLowercase = options.Password.RequireNonAlphanumeric = false;
        }).AddEntityFrameworkStores<CrmDbContext>();
        // Appending would leave the default UTF-16/whitespace policy active as well.
        var defaultValidators = services.Where(x => x.ServiceType == typeof(IPasswordValidator<LocalUser>)).ToArray();
        foreach (var registration in defaultValidators) services.Remove(registration);
        services.AddScoped<IPasswordValidator<LocalUser>, ScalarPasswordValidator>();
        services.AddScoped<IUserValidator<LocalUser>, LocalAccountValidator>();
        return services;
    }
}
