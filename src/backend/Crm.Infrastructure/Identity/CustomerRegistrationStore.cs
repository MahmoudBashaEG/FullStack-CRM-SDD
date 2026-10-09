using Crm.Application.Accounts.Contracts;
using Crm.Application.Accounts.Ports;
using Crm.Application.Common;
using Crm.Domain.Accounts;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace Crm.Infrastructure.Identity;

public sealed class CustomerRegistrationStore(CrmDbContext db,UserManager<LocalUser> users) : IAccountRegistrationStore
{
    public async Task<OperationResult<AccountIdentity>> RegisterAsync(NewAccount account,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Defense in depth: this public-registration port cannot create a staff account.
        if(account.Role!=AccountRole.Customer)return OperationResult<AccountIdentity>.Fail("registration_failed",400);
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        try
        {
            var user=new LocalUser {DisplayName=account.DisplayName,Email=account.Email,UserName=account.Email,AccountRole=AccountRole.Customer,IsActive=true,AuthVersion=1};
            var created=await users.CreateAsync(user,account.Password);
            ct.ThrowIfCancellationRequested();
            if(!created.Succeeded)
            {
                await transaction.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();
                return OperationResult<AccountIdentity>.Fail("registration_failed",400);
            }
            await transaction.CommitAsync(ct);
            return OperationResult<AccountIdentity>.Success(new(user.Id,user.DisplayName,AccountRole.Customer));
        }
        catch(DbUpdateException error) when(error.InnerException is SqlException {Number:2601 or 2627})
        {
            await transaction.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();
            return OperationResult<AccountIdentity>.Fail("registration_failed",400);
        }
        // Disposal rolls back cancellation/other failures. Never retry an uncertain commit.
    }
}
