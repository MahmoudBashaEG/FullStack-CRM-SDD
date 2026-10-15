using Crm.Application.Abstractions.Persistence;
using Crm.Domain.Identity;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crm.Infrastructure.Identity;

/// <summary>Idempotent: creates the three roles and the initial SuperAdmin when absent. Never resets or promotes.</summary>
internal sealed class IdentityBootstrapper(
    CrmDbContext dbContext,
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<ApplicationUser> userManager,
    IUnitOfWork unitOfWork,
    IOptions<BootstrapSuperAdminOptions> options,
    TimeProvider timeProvider,
    ILogger<IdentityBootstrapper> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            logger.LogWarning(
                "Identity bootstrap skipped: {Count} pending migration(s). Run 'dotnet ef database update' (README) and restart.",
                pending.Count);
            return;
        }

        foreach (var role in UserRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole<Guid>(role)), $"create role {role}");
            }
        }

        var admin = options.Value;
        var existing = await userManager.FindByEmailAsync(admin.Email);
        if (existing is not null)
        {
            if (!await userManager.IsInRoleAsync(existing, UserRoles.SuperAdmin))
            {
                logger.LogError(
                    "Bootstrap configuration issue: the configured SuperAdmin email belongs to an account without the SuperAdmin role. It was NOT promoted; resolve manually.");
            }

            return; // never recreate, never reset the password
        }

        if (string.IsNullOrWhiteSpace(admin.Password))
        {
            throw new InvalidOperationException(
                "The initial SuperAdmin account does not exist and 'Bootstrap:SuperAdmin:Password' is not configured. " +
                "Run: dotnet user-secrets set \"Bootstrap:SuperAdmin:Password\" \"<initial-superadmin-password>\" --project src/Crm.Api (see README), then restart.");
        }

        await unitOfWork.ExecuteInTransactionAsync(async _ =>
        {
            var user = new ApplicationUser
            {
                Id = Guid.CreateVersion7(),
                UserName = admin.Email,
                Email = admin.Email,
                FirstName = admin.FirstName,
                LastName = admin.LastName,
                IsActive = true,
                CreatedAtUtc = timeProvider.GetUtcNow(),
            };
            EnsureSucceeded(await userManager.CreateAsync(user, admin.Password), "create the SuperAdmin account");
            EnsureSucceeded(await userManager.AddToRoleAsync(user, UserRoles.SuperAdmin), "assign the SuperAdmin role");
            return true;
        }, cancellationToken);

        logger.LogInformation("Initial SuperAdmin account provisioned.");
    }

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            // Identity descriptions never contain the password.
            throw new InvalidOperationException(
                $"Identity bootstrap could not {action}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }
}
