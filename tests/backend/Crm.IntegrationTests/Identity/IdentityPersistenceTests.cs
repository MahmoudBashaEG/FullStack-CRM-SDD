using Crm.Domain.Accounts;
using Crm.Infrastructure;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Authentication;
using Crm.Infrastructure.Persistence;
using Crm.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Crm.IntegrationTests.Identity;

[Collection("SQL")]
public sealed class IdentityPersistenceTests(SqlServerFixture sql)
{
    private ServiceProvider Services() => new ServiceCollection().AddLogging().AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["ConnectionStrings:Crm"]=sql.Connection }).Build()).BuildServiceProvider();
    internal static LocalUser User(AccountRole role=AccountRole.Agent)
    {
        var user=new LocalUser {DisplayName="مريم",AccountRole=role}; user.Email=user.UserName=user.Id.ToString("N")+"@fixture.test"; return user;
    }
    [Fact] public async Task Standard_hashing_accepts_exact_spaces_unicode_and_rejects_changed_password()
    {
        using var provider=Services(); using var scope=provider.CreateScope(); var manager=scope.ServiceProvider.GetRequiredService<UserManager<LocalUser>>();
        Assert.IsType<PasswordHasher<LocalUser>>(manager.PasswordHasher); Assert.IsType<ScalarPasswordValidator>(Assert.Single(manager.PasswordValidators));
        foreach(var password in new[]{new string(' ',15),string.Concat(Enumerable.Repeat("😀",128)),"  Secret عربي  123  "})
        {
            var user=User(); Assert.True((await manager.CreateAsync(user,password)).Succeeded);
            Assert.True(await manager.CheckPasswordAsync(user,password)); Assert.False(await manager.CheckPasswordAsync(user,password+"x"));
            Assert.False(user.PasswordHash==password); Assert.False(string.IsNullOrEmpty(user.PasswordHash));
            if(password!=password.Trim()) Assert.False(await manager.CheckPasswordAsync(user,password.Trim()));
        }
        foreach(var password in new[]{new string('x',14),new string('x',129),new string('\ud800',15)}) Assert.False((await manager.CreateAsync(User(),password)).Succeeded);
    }
    [Fact] public async Task Versioned_framework_hash_can_be_rehashed()
    {
        using var provider=Services(); using var scope=provider.CreateScope(); var manager=scope.ServiceProvider.GetRequiredService<UserManager<LocalUser>>();
        var user=User(); var password=Guid.NewGuid().ToString("N");
        var older=new PasswordHasher<LocalUser>(Microsoft.Extensions.Options.Options.Create(new PasswordHasherOptions {IterationCount=10000}));
        user.PasswordHash=older.HashPassword(user,password);
        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded,manager.PasswordHasher.VerifyHashedPassword(user,user.PasswordHash,password));
        Assert.True((await manager.CreateAsync(user)).Succeeded); var before=user.PasswordHash;
        Assert.True(await manager.CheckPasswordAsync(user,password)); Assert.False(before==user.PasswordHash);
    }
    [Fact] public async Task Racing_email_creations_cannot_create_duplicates()
    {
        var email=Guid.NewGuid().ToString("N")+"@fixture.test";
        async Task<bool> Create()
        {
            using var provider=Services();using var scope=provider.CreateScope();var manager=scope.ServiceProvider.GetRequiredService<UserManager<LocalUser>>();var user=User();user.UserName=user.Email=email;
            try{return(await manager.CreateAsync(user,Guid.NewGuid().ToString("N"))).Succeeded;}
            catch(DbUpdateException error) when(error.InnerException is SqlException {Number:2601 or 2627}){return false;}
        }
        Assert.Equal(1,(await Task.WhenAll(Create(),Create())).Count(x=>x));
        await using var db=sql.OpenContext(); Assert.Equal(1,await db.Users.CountAsync(x=>x.NormalizedEmail==email.ToUpperInvariant()));
    }
    [Fact] public async Task Database_rejects_invalid_role_and_second_current_refresh_and_account_delete()
    {
        using var provider=Services();using var scope=provider.CreateScope(); var manager=scope.ServiceProvider.GetRequiredService<UserManager<LocalUser>>();var user=User(); Assert.True((await manager.CreateAsync(user,Guid.NewGuid().ToString("N"))).Succeeded);
        await using(var db=sql.OpenContext()){var same=await db.Users.SingleAsync(x=>x.Id==user.Id);same.AccountRole=(AccountRole)99;await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());}
        var session=new AuthenticationSession {UserId=user.Id,AuthVersionAtCreation=1,CreatedAtUtc=DateTimeOffset.UtcNow,ExpiresAtUtc=DateTimeOffset.UtcNow.AddDays(7)};
        await using(var db=sql.OpenContext()){db.AuthenticationSessions.Add(session);await db.SaveChangesAsync();db.RefreshCredentials.Add(new(){SessionId=session.Id,TokenHash=System.Security.Cryptography.RandomNumberGenerator.GetBytes(32),CreatedAtUtc=session.CreatedAtUtc,ExpiresAtUtc=session.ExpiresAtUtc});await db.SaveChangesAsync();}
        await using(var db=sql.OpenContext()){db.RefreshCredentials.Add(new(){SessionId=session.Id,TokenHash=System.Security.Cryptography.RandomNumberGenerator.GetBytes(32),CreatedAtUtc=session.CreatedAtUtc,ExpiresAtUtc=session.ExpiresAtUtc});await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());}
        await using(var db=sql.OpenContext()){db.Users.Remove(await db.Users.SingleAsync(x=>x.Id==user.Id));await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());}
    }
    [Fact] public async Task Account_rowversion_conflicts_are_detected()
    {
        using var provider=Services();using var scope=provider.CreateScope();var user=User();Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<LocalUser>>().CreateAsync(user,Guid.NewGuid().ToString("N"))).Succeeded);
        await using var first=sql.OpenContext();await using var second=sql.OpenContext();var a=await first.Users.SingleAsync(x=>x.Id==user.Id);var b=await second.Users.SingleAsync(x=>x.Id==user.Id);
        a.DisplayName="First";await first.SaveChangesAsync();b.DisplayName="Stale";await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>second.SaveChangesAsync());
    }
}
