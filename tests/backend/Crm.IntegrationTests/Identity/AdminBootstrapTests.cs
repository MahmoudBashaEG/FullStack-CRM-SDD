using Crm.Application;
using Crm.Application.Accounts.Commands;
using Crm.Domain.Accounts;
using Crm.Infrastructure;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Persistence;
using Crm.IntegrationTests.Fixtures;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Crm.IntegrationTests.Identity;

[Collection("SQL")]
public sealed class AdminBootstrapTests
{
    internal static ServiceProvider Services(string connection, SaveChangesInterceptor? interceptor = null)
    {
        var services = new ServiceCollection().AddLogging().AddApplication().AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["ConnectionStrings:Crm"] = connection }).Build());
        if(interceptor is not null) services.AddDbContext<CrmDbContext>(options => options.AddInterceptors(interceptor));
        return services.BuildServiceProvider();
    }
    internal static BootstrapInitialAdminCommand Command(string? password = "123456789012345", string email = "admin@fixture.test") => new(_ => Task.FromResult<AdminBootstrapCredentials?>(new("Operator",email,password)));
    [Fact] public async Task Racing_and_repeated_bootstrap_create_one_Admin_and_preserve_every_field()
    {
        await using var sql = await MigrationStartupFixture.CreateAsync(StartupSchema.Current);
        async Task<Crm.Application.Common.OperationResult<AdminBootstrapResult>> Run()
        { using var provider=Services(sql.Connection);using var scope=provider.CreateScope();return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(Command()); }
        var results=await Task.WhenAll(Run(),Run(),Run());
        Assert.All(results,x=>Assert.True(x.Succeeded));Assert.Single(results,x=>x.Value!.Status=="created");Assert.Single(results.Select(x=>x.Value!.UserId).Distinct());
        await using var db=sql.OpenContext();var user=Assert.Single(await db.Users.AsNoTracking().ToListAsync());var before=System.Text.Json.JsonSerializer.Serialize(user);
        Assert.Equal(AccountRole.Admin,user.AccountRole);Assert.True(user.IsActive);Assert.Equal(1,user.AuthVersion);Assert.NotEmpty(user.SecurityStamp!);Assert.NotEmpty(user.RowVersion);
        Assert.Single(await db.InitialAdminSetup.ToListAsync());Assert.Empty(await db.Agents.ToListAsync());Assert.Empty(await db.AuthenticationSessions.ToListAsync());
        using var provider=Services(sql.Connection);using var scope=provider.CreateScope();Assert.True(await scope.ServiceProvider.GetRequiredService<UserManager<LocalUser>>().CheckPasswordAsync(user,"123456789012345"));
        var repeated=await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new BootstrapInitialAdminCommand(_=>throw new Exception("Must not prompt")));
        Assert.Equal("already_complete",repeated.Value!.Status);
        Assert.Equal(before,System.Text.Json.JsonSerializer.Serialize(await db.Users.AsNoTracking().SingleAsync()));
    }
    [Theory] [InlineData(AccountRole.Customer)] [InlineData(AccountRole.Agent)] [InlineData(AccountRole.Admin)]
    public async Task Conflicting_email_never_promotes_or_overwrites(AccountRole role)
    {
        await using var sql=await MigrationStartupFixture.CreateAsync(StartupSchema.Current);using var provider=Services(sql.Connection);using var scope=provider.CreateScope();
        var user=IdentityPersistenceTests.User(role);user.Email=user.UserName="used@fixture.test";Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<LocalUser>>().CreateAsync(user,"123456789012345")).Succeeded);
        var before=System.Text.Json.JsonSerializer.Serialize(user);
        Assert.False((await scope.ServiceProvider.GetRequiredService<IMediator>().Send(Command(email:" USED@fixture.test "))).Succeeded);
        await using var db=sql.OpenContext();Assert.Equal(before,System.Text.Json.JsonSerializer.Serialize(await db.Users.SingleAsync()));Assert.Empty(await db.InitialAdminSetup.ToListAsync());
    }
    [Theory] [InlineData(14,false)] [InlineData(15,true)] [InlineData(128,true)] [InlineData(129,false)]
    public async Task Scalar_password_boundaries_and_spaces_are_exact(int length,bool accepted)
    {
        foreach(var password in new[]{new string(' ',length),string.Concat(Enumerable.Repeat("😀",length))})
        {
            await using var sql=await MigrationStartupFixture.CreateAsync(StartupSchema.Current);using var provider=Services(sql.Connection);using var scope=provider.CreateScope();
            Assert.Equal(accepted,(await scope.ServiceProvider.GetRequiredService<IMediator>().Send(Command(password))).Succeeded);
            await using var db=sql.OpenContext();Assert.Equal(accepted?1:0,await db.Users.CountAsync());Assert.Equal(accepted?1:0,await db.InitialAdminSetup.CountAsync());
            if(accepted){var user=await db.Users.SingleAsync();Assert.True(await scope.ServiceProvider.GetRequiredService<UserManager<LocalUser>>().CheckPasswordAsync(user,password));Assert.NotEqual(password,user.PasswordHash);}
        }
    }
    private sealed class FailMarker(bool cancel) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {
            if(data.Context!.ChangeTracker.Entries<InitialAdminSetup>().Any(x=>x.State==EntityState.Added)) { if(cancel)throw new OperationCanceledException();throw new InvalidOperationException("Injected storage failure"); }
            return base.SavingChangesAsync(data,result,ct);
        }
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Failure_after_Identity_save_rolls_back_user_and_completion(bool cancel)
    {
        await using var sql=await MigrationStartupFixture.CreateAsync(StartupSchema.Current);using var provider=Services(sql.Connection,new FailMarker(cancel));using var scope=provider.CreateScope();
        await Assert.ThrowsAnyAsync<Exception>(()=>scope.ServiceProvider.GetRequiredService<IMediator>().Send(Command()));
        await using var db=sql.OpenContext();Assert.Empty(await db.Users.ToListAsync());Assert.Empty(await db.InitialAdminSetup.ToListAsync());
        using var clean=Services(sql.Connection);using var retry=clean.CreateScope();Assert.True((await retry.ServiceProvider.GetRequiredService<IMediator>().Send(Command())).Succeeded);
    }
    [Fact] public async Task Missing_credentials_and_absent_schema_have_no_fallback()
    {
        await using var sql=await MigrationStartupFixture.CreateAsync(StartupSchema.Current);using var provider=Services(sql.Connection);using var scope=provider.CreateScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<IMediator>().Send(Command(null))).Succeeded);
        await using var db=sql.OpenContext();Assert.Empty(await db.Users.ToListAsync());Assert.Empty(await db.InitialAdminSetup.ToListAsync());
        await using var empty=await MigrationStartupFixture.CreateAsync(StartupSchema.Fresh);using var bad=Services(empty.Connection);using var badScope=bad.CreateScope();
        await Assert.ThrowsAnyAsync<Exception>(()=>badScope.ServiceProvider.GetRequiredService<IMediator>().Send(Command()));Assert.Empty(await empty.AppliedMigrationsAsync());
    }
    [Fact] public async Task Cancelled_credential_input_never_creates_an_account()
    {
        await using var sql=await MigrationStartupFixture.CreateAsync(StartupSchema.Current);using var provider=Services(sql.Connection);using var scope=provider.CreateScope();using var cancellation=new CancellationTokenSource();
        var command=new BootstrapInitialAdminCommand(_=>{cancellation.Cancel();return Task.FromResult<AdminBootstrapCredentials?>(new("Operator","admin@fixture.test","123456789012345"));});
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>scope.ServiceProvider.GetRequiredService<IMediator>().Send(command,cancellation.Token));
        await using var db=sql.OpenContext();Assert.Empty(await db.Users.ToListAsync());Assert.Empty(await db.InitialAdminSetup.ToListAsync());
    }
}
