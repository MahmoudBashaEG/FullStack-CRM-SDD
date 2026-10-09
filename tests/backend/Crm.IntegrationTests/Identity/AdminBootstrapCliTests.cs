using System.Text.Json;
using Crm.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Crm.Infrastructure.Identity;
namespace Crm.IntegrationTests.Identity;

[Collection("SQL")]
public sealed class AdminBootstrapCliTests
{
    [Fact] public async Task Native_first_repeat_and_concurrent_invocations_use_private_stdin_without_signer_or_HTTP()
    {
        await using var sql=await MigrationStartupFixture.CreateAsync(StartupSchema.Current);var password=Guid.NewGuid().ToString("N");
        var outcomes=await Task.WhenAll(Enumerable.Range(0,3).Select(_=>BootstrapCliFixture.RunAsync(sql.Connection,BootstrapCliFixture.Input(password))));
        Assert.All(outcomes,x=>{Assert.Equal(0,x.ExitCode);Assert.Empty(x.Error);Assert.DoesNotContain(password,x.Output);Assert.DoesNotContain("operator@fixture.test",x.Output);});
        var documents=outcomes.Select(x=>JsonDocument.Parse(x.Output)).ToArray();try
        {
            Assert.Single(documents,x=>x.RootElement.GetProperty("status").GetString()=="created");
            Assert.Single(documents.Select(x=>x.RootElement.GetProperty("userId").GetGuid()).Distinct());
            Assert.All(documents,x=>Assert.Equal(2,x.RootElement.EnumerateObject().Count()));
        }finally{foreach(var doc in documents)doc.Dispose();}
        var repeated=await BootstrapCliFixture.RunAsync(sql.Connection,null,["bootstrap-initial-admin"]);
        Assert.Equal(0,repeated.ExitCode);Assert.Empty(repeated.Error);Assert.Contains("already_complete",repeated.Output);
        var ignoredInput=await BootstrapCliFixture.RunAsync(sql.Connection,"invalid JSON which must not be read");Assert.Equal(0,ignoredInput.ExitCode);Assert.Contains("already_complete",ignoredInput.Output);Assert.Empty(ignoredInput.Error);
        await using var db=sql.OpenContext();Assert.Single(await db.Users.ToListAsync());Assert.Single(await db.InitialAdminSetup.ToListAsync());Assert.Equal(4,(await sql.AppliedMigrationsAsync()).Length);
    }
    [Theory] [InlineData(null)] [InlineData("")] [InlineData("{}")]
    [InlineData("{\"displayName\":\"Admin\",\"email\":\"a@fixture.test\",\"password\":\"short\"}")]
    [InlineData("{\"displayName\":\"Admin\",\"email\":\"a@fixture.test\",\"password\":\"123456789012345\",\"role\":\"Admin\"}")]
    [InlineData("{\"displayName\":\"Admin\",\"email\":\"a@fixture.test\",\"password\":\"123456789012345\",\"password\":\"123456789012345\"}")]
    public async Task Invalid_or_missing_private_input_exits_two_without_partial_state(string? input)
    {
        await using var sql=await MigrationStartupFixture.CreateAsync(StartupSchema.Current);var result=await BootstrapCliFixture.RunAsync(sql.Connection,input);
        Assert.Equal(2,result.ExitCode);Assert.Empty(result.Output);Assert.Equal("Invalid bootstrap input or identity conflict.",result.Error.Trim());
        await using var db=sql.OpenContext();Assert.Empty(await db.Users.ToListAsync());Assert.Empty(await db.InitialAdminSetup.ToListAsync());
    }
    [Fact] public async Task Missing_schema_in_Development_exits_three_without_migrating_or_fallback()
    {
        await using var sql=await MigrationStartupFixture.CreateAsync(StartupSchema.Fresh);var password=Guid.NewGuid().ToString("N");
        var result=await BootstrapCliFixture.RunAsync(sql.Connection,BootstrapCliFixture.Input(password),environment:"Development");
        Assert.Equal(3,result.ExitCode);Assert.Empty(result.Output);Assert.DoesNotContain(password,result.Error);Assert.Empty(await sql.AppliedMigrationsAsync());Assert.Equal(0,await sql.ObjectExistsAsync("dbo.CrmUsers"));
    }
    [Fact] public async Task Password_arguments_and_redirected_interactive_input_are_rejected()
    {
        await using var sql=await MigrationStartupFixture.CreateAsync(StartupSchema.Current);
        var argv=await BootstrapCliFixture.RunAsync(sql.Connection,null,["bootstrap-initial-admin","--password","not-a-credential"]);Assert.Equal(2,argv.ExitCode);Assert.Empty(argv.Output);
        var interactive=await BootstrapCliFixture.RunAsync(sql.Connection,BootstrapCliFixture.Input(Guid.NewGuid().ToString("N")),["bootstrap-initial-admin"]);Assert.Equal(2,interactive.ExitCode);Assert.Empty(interactive.Output);
        await using var db=sql.OpenContext();Assert.Empty(await db.Users.ToListAsync());
    }
    [Fact] public async Task Native_conflict_reports_safe_exit_two_and_leaves_existing_identity_unchanged()
    {
        await using var sql=await MigrationStartupFixture.CreateAsync(StartupSchema.Current);using var provider=AdminBootstrapTests.Services(sql.Connection);using var scope=provider.CreateScope();
        var user=IdentityPersistenceTests.User(Crm.Domain.Accounts.AccountRole.Customer);user.Email=user.UserName="operator@fixture.test";
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<LocalUser>>().CreateAsync(user,Guid.NewGuid().ToString("N"))).Succeeded);
        var before=JsonSerializer.Serialize(user);var password=Guid.NewGuid().ToString("N");var result=await BootstrapCliFixture.RunAsync(sql.Connection,BootstrapCliFixture.Input(password));
        Assert.Equal(2,result.ExitCode);Assert.Empty(result.Output);Assert.Equal("Invalid bootstrap input or identity conflict.",result.Error.Trim());Assert.DoesNotContain(password,result.Error);
        await using var db=sql.OpenContext();Assert.Equal(before,JsonSerializer.Serialize(await db.Users.SingleAsync()));Assert.Empty(await db.InitialAdminSetup.ToListAsync());
    }
    [Fact] public async Task Documented_dotnet_run_invocation_works_in_Development_without_signer()
    {
        await using var sql=await MigrationStartupFixture.CreateAsync(StartupSchema.Current);
        var result=await BootstrapCliFixture.RunAsync(sql.Connection,BootstrapCliFixture.Input(Guid.NewGuid().ToString("N")),environment:"Development",viaRun:true);
        Assert.Equal(0,result.ExitCode);Assert.Empty(result.Error);using var document=JsonDocument.Parse(result.Output);Assert.Equal("created",document.RootElement.GetProperty("status").GetString());
        var repeated=await BootstrapCliFixture.RunAsync(sql.Connection,null,["bootstrap-initial-admin"],environment:"Development",viaRun:true);
        Assert.Equal(0,repeated.ExitCode);Assert.Empty(repeated.Error);Assert.Contains("already_complete",repeated.Output);
    }
}
