using System.Net;
using Crm.Infrastructure.Persistence;
using Crm.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
namespace Crm.IntegrationTests.Identity;

[Collection("SQL")]
public sealed class FoundationMigrationStartupTests
{
    private sealed class Api(string connection,LocalAccountFixture signing,string environment) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        { builder.UseEnvironment(environment);builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(signing.Configuration(connection).AsEnumerable())); }
    }
    [Theory] [InlineData(StartupSchema.Fresh)] [InlineData(StartupSchema.InitialStorage)] [InlineData(StartupSchema.Current)]
    public async Task Development_applies_existing_pending_migrations_before_readiness_and_repeat_is_stable(StartupSchema state)
    {
        await using var sql=await MigrationStartupFixture.CreateAsync(state);using var signing=new LocalAccountFixture();
        await using(var api=new Api(sql.Connection,signing,"Development")){using var client=api.CreateClient();Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/ticket-reference-data/categories")).StatusCode);}
        var applied=await sql.AppliedMigrationsAsync();Assert.Equal(4,applied.Length);Assert.Contains("20261009124742_LocalCrmAuthentication",applied);
        Assert.EndsWith("_RefreshSessionLookupIndex",applied[^1]);
        await using(var api=new Api(sql.Connection,signing,"Development")){using var client=api.CreateClient();Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/ticket-reference-data/categories")).StatusCode);}
        Assert.Equal(applied,await sql.AppliedMigrationsAsync());await using var db=sql.OpenContext();Assert.False(db.Database.HasPendingModelChanges());
    }
    [Theory] [InlineData("Production")] [InlineData("Staging")] [InlineData("Testing")] [InlineData("Preview")]
    public async Task Other_environments_do_not_implicitly_migrate(string environment)
    {
        await using var sql=await MigrationStartupFixture.CreateAsync(StartupSchema.Fresh);using var signing=new LocalAccountFixture();
        await using var api=new Api(sql.Connection,signing,environment);using var client=api.CreateClient();Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/ticket-reference-data/categories")).StatusCode);
        Assert.Empty(await sql.AppliedMigrationsAsync());Assert.Equal(0,await sql.ObjectExistsAsync("dbo.CrmUsers"));
    }
}
