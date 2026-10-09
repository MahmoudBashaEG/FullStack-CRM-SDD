using Crm.Application.Accounts.Validation;
using Crm.Domain.Accounts;
using Microsoft.AspNetCore.Identity;
namespace Crm.Infrastructure.Identity;

public sealed class LocalAccountValidator : IUserValidator<LocalUser>
{
    public Task<IdentityResult> ValidateAsync(UserManager<LocalUser> manager, LocalUser user)
    {
        if (!AccountRules.DisplayNameAccepted(user.DisplayName) || !Enum.IsDefined(user.AccountRole) || user.AuthVersion < 1 || user.Id == Guid.Empty ||
            !AccountValidation.TryNormalizeEmail(user.Email, out var email, out _) || user.UserName != email)
            return Task.FromResult(IdentityResult.Failed(new IdentityError { Code="InvalidAccount", Description="Account details are invalid." }));
        user.DisplayName = user.DisplayName.Trim();
        return Task.FromResult(IdentityResult.Success);
    }
}
