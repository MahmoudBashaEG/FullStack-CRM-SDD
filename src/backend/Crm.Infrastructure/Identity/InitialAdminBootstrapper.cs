using System.Data;
using Crm.Application.Accounts.Commands;
using Crm.Application.Accounts.Contracts;
using Crm.Application.Accounts.Ports;
using Crm.Application.Common;
using Crm.Domain.Accounts;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace Crm.Infrastructure.Identity;

public sealed class InitialAdminBootstrapper(CrmDbContext db,UserManager<LocalUser> users,TimeProvider clock) : IInitialAdminBootstrapStore
{
    public async Task<OperationResult<AdminBootstrapResult>> BootstrapAsync(Func<CancellationToken,Task<OperationResult<NewAccount>>> input,CancellationToken cancellationToken)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,cancellationToken);
        try
        {
            // UPDLOCK on an absent singleton key holds its range: another installer waits,
            // rather than both acquiring shared locks then deadlocking on insertion.
            var setup=await db.InitialAdminSetup.FromSqlRaw("SELECT [Id],[AdminUserId],[CompletedAtUtc] FROM [dbo].[InitialAdminSetup] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]=1").SingleOrDefaultAsync(cancellationToken);
            if(setup is not null) return OperationResult<AdminBootstrapResult>.Success(new("already_complete",setup.AdminUserId));
            var supplied=await input(cancellationToken);
            if(!supplied.Succeeded) return new(null,supplied.Error);
            var account=supplied.Value!;
            if(account.Role!=AccountRole.Admin) return OperationResult<AdminBootstrapResult>.Fail("bootstrap_invalid",400);
            cancellationToken.ThrowIfCancellationRequested();
            if(await users.FindByEmailAsync(account.Email) is not null) return OperationResult<AdminBootstrapResult>.Fail("bootstrap_invalid",400);
            cancellationToken.ThrowIfCancellationRequested();
            var user=new LocalUser {DisplayName=account.DisplayName,Email=account.Email,UserName=account.Email,AccountRole=AccountRole.Admin,IsActive=true,AuthVersion=1};
            var created=await users.CreateAsync(user,account.Password);
            cancellationToken.ThrowIfCancellationRequested();
            if(!created.Succeeded) return OperationResult<AdminBootstrapResult>.Fail("bootstrap_invalid",400);
            db.InitialAdminSetup.Add(new() {AdminUserId=user.Id,CompletedAtUtc=clock.GetUtcNow()});
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return OperationResult<AdminBootstrapResult>.Success(new("created",user.Id));
        }
        catch(DbUpdateException error) when(error.InnerException is SqlException {Number:2601 or 2627})
        {
            // Identity uniqueness is authoritative; never promote/reset the conflicting user.
            await transaction.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();
            return OperationResult<AdminBootstrapResult>.Fail("bootstrap_invalid",400);
        }
        // Dispose rolls back all other failures/cancellation; no retry on uncertain commit.
    }
}
