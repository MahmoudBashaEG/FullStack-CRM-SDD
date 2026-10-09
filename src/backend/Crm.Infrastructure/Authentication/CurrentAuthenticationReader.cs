using Crm.Application.Authentication.Contracts;
using Crm.Application.Authentication.Ports;
using Crm.Application.Common;
using Crm.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace Crm.Infrastructure.Authentication;

public sealed class CurrentAuthenticationReader(CrmDbContext database, TimeProvider clock) : ICurrentAuthenticationReader
{
    public async Task<TrustedAuthentication?> ValidateAsync(AuthenticationBinding binding, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (binding.UserId == Guid.Empty || binding.SessionId == Guid.Empty || binding.AuthenticationVersion < 1 || !Enum.IsDefined(binding.Role)) return null;
        try
        {
            var now = clock.GetUtcNow();
            return await (from session in database.AuthenticationSessions.AsNoTracking()
                          join user in database.Users.AsNoTracking() on session.UserId equals user.Id
                          where session.Id == binding.SessionId && user.Id == binding.UserId && user.IsActive && user.AccountRole == binding.Role &&
                              user.AuthVersion == binding.AuthenticationVersion && session.AuthVersionAtCreation == binding.AuthenticationVersion && session.RevokedAtUtc == null && session.ExpiresAtUtc > now
                          select new TrustedAuthentication(new(user.Id,user.DisplayName,user.AccountRole),session.Id,user.AuthVersion,session.ExpiresAtUtc)).SingleOrDefaultAsync(cancellationToken);
        }
        catch (Exception error) when (error is SqlException || error is InvalidOperationException { InnerException: SqlException })
        { throw new DependencyUnavailableException(); }
    }
}
