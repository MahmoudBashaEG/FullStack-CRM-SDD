using Crm.Domain.Accounts;
using Microsoft.AspNetCore.Identity;
namespace Crm.Infrastructure.Identity;

public sealed class ScalarPasswordValidator : IPasswordValidator<LocalUser>
{
    public Task<IdentityResult> ValidateAsync(UserManager<LocalUser> manager, LocalUser user, string? password) => Task.FromResult(
        AccountRules.PasswordAccepted(password) ? IdentityResult.Success : IdentityResult.Failed(new IdentityError { Code = "InvalidPassword", Description = "Password does not meet requirements." }));
}
