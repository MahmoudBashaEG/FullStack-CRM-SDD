using Crm.Application.Abstractions.Identity;

namespace Crm.Api.Contracts.Profile;

public sealed record CurrentUserResponse(Guid Id, string Email, string FirstName, string LastName, string? PhoneNumber, string Role)
{
    public static CurrentUserResponse From(UserAccount user) =>
        new(user.Id, user.Email, user.FirstName, user.LastName, user.PhoneNumber, user.Role);
}
