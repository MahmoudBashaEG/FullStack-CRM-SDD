using Crm.Application.Abstractions.Identity;
using Crm.Application.Common.Exceptions;
using Crm.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Identity;

internal sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    CrmDbContext dbContext,
    TimeProvider timeProvider) : IIdentityService
{
    // Verified against when the email is unknown, so unknown and known accounts cost the same time.
    private static readonly Lazy<string> DummyHash = new(() =>
        new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), Guid.NewGuid().ToString("N")));

    private static readonly ApplicationUser DummyUser = new();

    // Filter/order BEFORE projecting: EF cannot translate predicates over the constructed record.
    private IQueryable<UserAccount> Project(IQueryable<ApplicationUser> users) =>
        from u in users.AsNoTracking()
        let role = (from ur in dbContext.UserRoles
                    join r in dbContext.Roles on ur.RoleId equals r.Id
                    where ur.UserId == u.Id
                    select r.Name).FirstOrDefault()
        select new UserAccount(
            u.Id, u.Email!, u.FirstName, u.LastName, u.PhoneNumber, role ?? string.Empty, u.IsActive, u.SecurityStamp!, u.CreatedAtUtc);

    public Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Project(dbContext.Users.Where(u => u.Id == userId)).SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        await userManager.FindByEmailAsync(email) is not null;

    public async Task<UserAccount?> CheckCredentialsAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            userManager.PasswordHasher.VerifyHashedPassword(DummyUser, DummyHash.Value, password);
            return null;
        }

        if (!user.IsActive || await userManager.IsLockedOutAsync(user))
        {
            return null;
        }

        if (await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.ResetAccessFailedCountAsync(user);
            return await FindByIdAsync(user.Id, cancellationToken);
        }

        await userManager.AccessFailedAsync(user);
        return null;
    }

    public async Task<UserAccount> CreateUserAsync(
        NewUserAccount account, string password, string role, CancellationToken cancellationToken = default)
    {
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = account.Email,
            Email = account.Email,
            FirstName = account.FirstName,
            LastName = account.LastName,
            PhoneNumber = account.PhoneNumber,
            IsActive = true,
            CreatedAtUtc = timeProvider.GetUtcNow(),
        };

        ThrowIfFailed(await userManager.CreateAsync(user, password));
        ThrowIfFailed(await userManager.AddToRoleAsync(user, role));
        return (await FindByIdAsync(user.Id, cancellationToken))!;
    }

    public async Task<IReadOnlyList<UserAccount>> ListByRoleAsync(string role, CancellationToken cancellationToken = default) =>
        await Project(dbContext.Users
            .Where(u => dbContext.UserRoles.Any(ur => ur.UserId == u.Id
                && dbContext.Roles.Any(r => r.Id == ur.RoleId && r.Name == role)))
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName))
            .ToListAsync(cancellationToken);

    public async Task<UserAccount> UpdateProfileAsync(Guid userId, UserProfileUpdate update, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("Account not found.");

        user.FirstName = update.FirstName;
        user.LastName = update.LastName;
        user.PhoneNumber = update.PhoneNumber;
        ThrowIfFailed(await userManager.UpdateAsync(user));

        return (await FindByIdAsync(userId, cancellationToken))!;
    }

    public async Task<bool> ChangePasswordAsync(
        Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("Account not found.");

        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
        {
            return false;
        }

        ThrowIfFailed(result);
        return true;
    }

    public async Task SetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("Account not found.");

        if (await userManager.HasPasswordAsync(user))
        {
            ThrowIfFailed(await userManager.RemovePasswordAsync(user));
        }

        ThrowIfFailed(await userManager.AddPasswordAsync(user, newPassword));
    }

    public async Task SetActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("Account not found.");

        user.IsActive = isActive;
        ThrowIfFailed(await userManager.UpdateSecurityStampAsync(user));
    }

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure)))
        {
            throw new ConflictException("The account was changed by another request. Reload it and try again.");
        }

        // Identity descriptions never contain the submitted values.
        throw new ValidationException(result.Errors.Select(e => new ValidationFailure(
            e.Code.StartsWith("Password", StringComparison.Ordinal) ? "password"
                : e.Code.Contains("Email", StringComparison.Ordinal) || e.Code.Contains("UserName", StringComparison.Ordinal) ? "email"
                : string.Empty,
            e.Description)));
    }
}
