using Crm.Application.Accounts.Contracts;
using Crm.Application.Accounts.Ports;
using Crm.Application.Common;
using Crm.Domain.Accounts;
using Crm.Infrastructure.Authentication;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Tickets;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace Crm.Infrastructure.Identity;
public sealed class AgentAccountStore(CrmDbContext db,UserManager<LocalUser> users,TimeProvider clock):IAgentAccountStore
{
    private static AgentAccountDto Dto(LocalUser user,AgentReference reference)=>new(user.Id,user.DisplayName,user.Email!,user.IsActive,reference.Id,Convert.ToBase64String(user.RowVersion));
    private sealed class AgentRow {public required LocalUser User{get;init;}public required AgentReference Reference{get;init;}}
    private IQueryable<AgentRow> Read()=>from user in db.Users.AsNoTracking() join reference in db.Agents.AsNoTracking() on user.Id equals reference.LocalUserId where user.AccountRole==AccountRole.Agent select new AgentRow{User=user,Reference=reference};
    public async Task<OperationResult<AgentAccountDto>> GetAsync(Guid id,CancellationToken ct)
    {var agent=await Read().SingleOrDefaultAsync(x=>x.User.Id==id,ct);return agent is null?OperationResult<AgentAccountDto>.Fail("agent_not_found",404):OperationResult<AgentAccountDto>.Success(Dto(agent.User,agent.Reference));}
    public async Task<AgentPage> ListAsync(int page,int pageSize,CancellationToken ct)
    {
        var query=Read();var count=await query.CountAsync(ct);var offset=((long)page-1)*pageSize;
        var items=offset>int.MaxValue?[]:await query.OrderBy(x=>x.User.DisplayName).ThenBy(x=>x.User.Id).Skip((int)offset).Take(pageSize).ToListAsync(ct);
        return new(items.Select(x=>Dto(x.User,x.Reference)).ToList(),page,pageSize,count);
    }
    public async Task<OperationResult<AgentAccountDto>> CreateAsync(NewAccount account,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();if(account.Role!=AccountRole.Agent)return OperationResult<AgentAccountDto>.Fail("validation_failed",400);
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        try
        {
            var user=new LocalUser{DisplayName=account.DisplayName,Email=account.Email,UserName=account.Email,AccountRole=AccountRole.Agent,IsActive=true,AuthVersion=1};
            var result=await users.CreateAsync(user,account.Password);ct.ThrowIfCancellationRequested();
            if(!result.Succeeded){await transaction.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();return OperationResult<AgentAccountDto>.Fail(result.Errors.Any(x=>x.Code is "DuplicateEmail" or "DuplicateUserName")?"agent_conflict":"validation_failed",result.Errors.Any(x=>x.Code is "DuplicateEmail" or "DuplicateUserName")?409:400);}
            var reference=new AgentReference{Id=Guid.NewGuid(),LocalUserId=user.Id,Issuer="urn:crm:local:user",Subject=user.Id.ToString("D"),DisplayName=user.DisplayName,IsEligible=true};db.Agents.Add(reference);await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return OperationResult<AgentAccountDto>.Success(Dto(user,reference));
        }
        catch(DbUpdateException error) when(error.InnerException is SqlException{Number:2601 or 2627}){await transaction.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();return OperationResult<AgentAccountDto>.Fail("agent_conflict",409);}
    }
    public Task<OperationResult<AgentAccountDto>> RenameAsync(Guid id,string name,byte[] version,CancellationToken ct)=>UpdateAsync(id,name,null,version,ct);
    public Task<OperationResult<AgentAccountDto>> SetAvailabilityAsync(Guid id,bool active,byte[] version,CancellationToken ct)=>UpdateAsync(id,null,active,version,ct);
    private async Task<OperationResult<AgentAccountDto>> UpdateAsync(Guid id,string? name,bool? active,byte[] version,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        try
        {
            var user=await db.Users.FromSqlInterpolated($"SELECT * FROM dbo.CrmUsers WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}").SingleOrDefaultAsync(ct);
            if(user is null||user.AccountRole!=AccountRole.Agent)return OperationResult<AgentAccountDto>.Fail("agent_not_found",404);
            if(!user.RowVersion.SequenceEqual(version))return OperationResult<AgentAccountDto>.Fail("account_changed",409);
            // All auth writers lock the account first. Disable then locks its families,
            // before taking the assignment-reference lock; no inverse account lock.
            var sessions=active==false&&user.IsActive?await db.AuthenticationSessions.FromSqlInterpolated($"SELECT * FROM dbo.AuthenticationSessions WITH (UPDLOCK,HOLDLOCK) WHERE UserId={id}").OrderBy(x=>x.Id).ToListAsync(ct):[];
            var reference=await db.Agents.FromSqlInterpolated($"SELECT * FROM dbo.AgentReferences WITH (UPDLOCK,HOLDLOCK) WHERE LocalUserId={id}").SingleOrDefaultAsync(ct);
            if(reference is null)return OperationResult<AgentAccountDto>.Fail("agent_not_found",404);
            if((name is null||name==user.DisplayName)&&(active is null||active==user.IsActive)){await transaction.CommitAsync(ct);return OperationResult<AgentAccountDto>.Success(Dto(user,reference));}
            if(name is not null){user.DisplayName=name;reference.DisplayName=name;}
            if(active.HasValue)
            {
                if(!active.Value){user.AuthVersion=checked(user.AuthVersion+1);user.SecurityStamp=Guid.NewGuid().ToString("N");foreach(var session in sessions.Where(x=>x.RevokedAtUtc is null)){session.RevokedAtUtc=clock.GetUtcNow();session.RevocationReason=SessionRevocationReason.AccountDisabled;}}
                user.IsActive=active.Value;reference.IsEligible=active.Value;
            }
            ct.ThrowIfCancellationRequested();var updated=await users.UpdateAsync(user);ct.ThrowIfCancellationRequested();
            if(!updated.Succeeded){await transaction.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();return OperationResult<AgentAccountDto>.Fail(updated.Errors.Any(x=>x.Code=="ConcurrencyFailure")?"account_changed":"validation_failed",updated.Errors.Any(x=>x.Code=="ConcurrencyFailure")?409:400);}
            await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return OperationResult<AgentAccountDto>.Success(Dto(user,reference));
        }
        catch(DbUpdateConcurrencyException){await transaction.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();return OperationResult<AgentAccountDto>.Fail("account_changed",409);}
    }
}
