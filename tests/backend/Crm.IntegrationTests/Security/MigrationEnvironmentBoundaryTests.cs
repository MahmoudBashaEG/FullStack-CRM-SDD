using System.Net;
using System.Text.Json;
using Crm.Infrastructure.Persistence;
using Crm.IntegrationTests.Fixtures;
using Crm.IntegrationTests.Tickets;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crm.IntegrationTests.Security;

[Collection("SQL")]
public sealed class MigrationEnvironmentBoundaryTests
{
    [Theory]
    [InlineData("Production")] [InlineData("Staging")] [InlineData("Testing")] [InlineData("Preview")]
    public async Task Non_Development_hosts_start_with_valid_auth_but_never_apply_pending_migrations(string environment)
    {
        await using var sql = await MigrationStartupFixture.CreateAsync(StartupSchema.InitialStorage);
        var sources = MigrationStartupFixture.SourceFingerprints();
        using var jwt = new TestJwtFactory(); await using var api = new MigrationStartupApiFactory(sql, jwt, environment: environment);
        using var client = api.CreateClient();
        Assert.Empty(api.Probe.Commands); Assert.Empty(api.Probe.Contexts); // Before any HTTP read.
        var options = api.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
        Assert.True(options.RequireHttpsMetadata); Assert.True(options.TokenValidationParameters.ValidateIssuerSigningKey);
        Assert.True(options.TokenValidationParameters.ValidateIssuer); Assert.True(options.TokenValidationParameters.ValidateAudience);
        Assert.True(options.TokenValidationParameters.ValidateLifetime);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/ticket-reference-data/categories")).StatusCode);
        // A genuine JWT issued from an updated isolated schema must still fail closed
        // against this unmigrated host: local account/session validation requires schema.
        await using var credentials=await MigrationStartupFixture.CreateAsync(StartupSchema.Current);jwt.Bind(credentials.Connection);
        client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/ticket-reference-data/categories")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token(allowed: false));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/ticket-reference-data/categories")).StatusCode);
        Assert.DoesNotContain(api.Probe.Commands, MigrationStartupProbe.IsMigrationWrite);
        Assert.Equal([MigrationStartupFixture.InitialMigration], await sql.AppliedMigrationsAsync());
        Assert.Equal(0, await sql.ObjectExistsAsync("dbo.TicketSubjectContainsLiteral"));
        await using var context = sql.OpenContext(); Assert.Empty(await context.Tickets.ToListAsync()); Assert.Empty(await context.History.ToListAsync());
        AssertSourcesUnchanged(sources);
    }

    [Theory]
    [InlineData(StartupSchema.Fresh, 3)] [InlineData(StartupSchema.InitialStorage, 0)]
    public async Task Development_provisioning_CLI_keeps_explicit_schema_prerequisite_and_does_not_migrate(StartupSchema state, int expectedExit)
    {
        await using var sql = await MigrationStartupFixture.CreateAsync(state); var sources = MigrationStartupFixture.SourceFingerprints();
        var before = await sql.AppliedMigrationsAsync(); var category = Guid.NewGuid();
        var input = JsonSerializer.Serialize(new { schemaVersion = 1, categories = new[] { new { id = category, displayName = "CLI boundary category", isAvailable = true } } });
        var result = await ReferenceProvisioningTests.RunCliAsync(sql.Connection, input, "Development");
        Assert.Equal(expectedExit, result.Exit); Assert.Equal(before, await sql.AppliedMigrationsAsync());
        Assert.Equal(0, await sql.ObjectExistsAsync("dbo.TicketSubjectContainsLiteral"));
        Assert.Equal(state == StartupSchema.Fresh ? 0 : 1, await sql.ObjectExistsAsync("dbo.Tickets"));
        if (state == StartupSchema.InitialStorage)
        {
            Assert.Equal(1, JsonDocument.Parse(result.Output).RootElement.GetProperty("inserted").GetInt32());
            await using var context = sql.OpenContext(); Assert.True(await context.Categories.AnyAsync(value => value.Id == category));
            Assert.Empty(await context.Tickets.ToListAsync()); Assert.Empty(await context.History.ToListAsync());
        }
        AssertSourcesUnchanged(sources);
    }

    [Theory]
    [InlineData(StartupSchema.Fresh)] [InlineData(StartupSchema.InitialStorage)]
    public async Task Development_design_time_construction_has_no_migration_side_effect_until_explicit_update(StartupSchema state)
    {
        await using var sql = await MigrationStartupFixture.CreateAsync(state); var sources = MigrationStartupFixture.SourceFingerprints();
        var before = await sql.AppliedMigrationsAsync();
        var keys = new[] { "ConnectionStrings__Crm", "DOTNET_ENVIRONMENT", "ASPNETCORE_ENVIRONMENT" };
        var previous = keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(keys[0], sql.Connection); Environment.SetEnvironmentVariable(keys[1], "Development"); Environment.SetEnvironmentVariable(keys[2], "Development");
            await using var context = new DesignTimeCrmDbContextFactory().CreateDbContext([]);
            Assert.Equal(CrmDatabaseSettings.Validate(sql.Connection), CrmDatabaseSettings.Validate(context.Database.GetDbConnection().ConnectionString));
            Assert.Equal(before, await sql.AppliedMigrationsAsync());
            Assert.Equal(0, await sql.ObjectExistsAsync("dbo.TicketSubjectContainsLiteral"));
            AssertSourcesUnchanged(sources);
            await context.Database.MigrateAsync(); // Explicit tooling-equivalent operation, not host startup.
            Assert.Equal(MigrationStartupFixture.CurrentMigrations, await sql.AppliedMigrationsAsync());
            Assert.Equal(1, await sql.ObjectExistsAsync("dbo.TicketSubjectContainsLiteral"));
            AssertSourcesUnchanged(sources);
        }
        finally { foreach (var key in keys) Environment.SetEnvironmentVariable(key, previous[key]); }
    }

    private static void AssertSourcesUnchanged(Dictionary<string, string> before) =>
        Assert.Equal(before.OrderBy(item => item.Key, StringComparer.Ordinal), MigrationStartupFixture.SourceFingerprints().OrderBy(item => item.Key, StringComparer.Ordinal));
}
