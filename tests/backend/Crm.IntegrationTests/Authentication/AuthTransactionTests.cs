using Crm.Application;
using Crm.Application.Authentication.Commands;
using Crm.Infrastructure;
using Crm.Infrastructure.Authentication;
using Crm.Infrastructure.Persistence;
using Crm.IntegrationTests.Fixtures;
using Crm.Domain.Accounts;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
namespace Crm.IntegrationTests.Authentication;

[Collection("SQL")]
public sealed class AuthTransactionTests
{
    private sealed class FailSuccessor(bool cancellation) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {
            if(data.Context!.ChangeTracker.Entries<RefreshCredential>().Any(x=>x.State==EntityState.Added))
            {if(cancellation)throw new OperationCanceledException();throw new InvalidOperationException("Owned successor fault.");}
            return base.SavingChangesAsync(data,result,ct);
        }
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Failed_refresh_rolls_back_consumption_and_failed_signin_leaves_no_family(bool cancellation)
    {
        await using var f=await AuthenticationApiFixture.CreateAsync();var password=Guid.NewGuid().ToString("N");var user=await f.PrepareAsync(AccountRole.Admin,password);await f.SignInAsync(user,password);var cookie=f.Cookies["__Host-crm-refresh"];
        using var services=new ServiceCollection().AddLogging().AddApplication().AddInfrastructure(f.Signing.Configuration(f.Sql.Connection)).AddDbContext<CrmDbContext>(options=>options.AddInterceptors(new FailSuccessor(cancellation))).BuildServiceProvider();
        using(var scope=services.CreateScope())await Assert.ThrowsAnyAsync<Exception>(()=>scope.ServiceProvider.GetRequiredService<IMediator>().Send(new RefreshAuthenticationCommand(cookie)));
        await using(var db=f.Sql.OpenContext()){Assert.Null((await db.RefreshCredentials.SingleAsync()).ConsumedAtUtc);Assert.Null((await db.AuthenticationSessions.SingleAsync()).RevokedAtUtc);}
        using(var scope=services.CreateScope())await Assert.ThrowsAnyAsync<Exception>(()=>scope.ServiceProvider.GetRequiredService<IMediator>().Send(new SignInCommand(user.Email,password,cookie)));
        await using(var db=f.Sql.OpenContext()){Assert.Equal(1,await db.AuthenticationSessions.CountAsync());Assert.Null((await db.AuthenticationSessions.SingleAsync()).RevokedAtUtc);}
        using var retry=await f.SendAsync("/api/auth/refresh",System.Net.Http.HttpMethod.Post,new{});Assert.Equal(System.Net.HttpStatusCode.OK,retry.StatusCode);
    }
}
