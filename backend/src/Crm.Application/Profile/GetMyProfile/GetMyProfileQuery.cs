using Crm.Application.Abstractions.Identity;
using Crm.Application.Common.Exceptions;
using Mediator;

namespace Crm.Application.Profile.GetMyProfile;

public sealed record GetMyProfileQuery(Guid UserId) : IQuery<UserAccount>;

public sealed class GetMyProfileQueryHandler(IIdentityService identity)
    : IQueryHandler<GetMyProfileQuery, UserAccount>
{
    public async ValueTask<UserAccount> Handle(GetMyProfileQuery query, CancellationToken cancellationToken) =>
        await identity.FindByIdAsync(query.UserId, cancellationToken)
            ?? throw new NotFoundException("Account not found.");
}
