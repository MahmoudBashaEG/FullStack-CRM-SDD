using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Crm.Domain.Tickets;
using Crm.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Crm.IntegrationTests.Persistence;

[Collection("SQL")]
public sealed class DevelopmentMigrationStartupTests
{
    private static readonly string[] ExpectedMigrations = [MigrationStartupFixture.InitialMigration, MigrationStartupFixture.SearchMigration];

    [Theory]
    [InlineData(StartupSchema.Fresh)] [InlineData(StartupSchema.InitialStorage)] [InlineData(StartupSchema.Current)]
    public async Task Fixture_preparation_preserves_the_requested_migration_state(StartupSchema state)
    {
        await using var sql = await MigrationStartupFixture.CreateAsync(state);
        var expected = state switch { StartupSchema.Fresh => Array.Empty<string>(), StartupSchema.InitialStorage => [MigrationStartupFixture.InitialMigration], _ => ExpectedMigrations };
        Assert.Equal(expected, await sql.AppliedMigrationsAsync());
        Assert.Equal(state == StartupSchema.Fresh ? 0 : 1, await sql.ObjectExistsAsync("dbo.Tickets"));
        Assert.Equal(state == StartupSchema.Current ? 1 : 0, await sql.ObjectExistsAsync("dbo.TicketSubjectContainsLiteral"));
        Assert.False(string.IsNullOrWhiteSpace(sql.WindowsAccount));
    }

    [Theory]
    [InlineData(StartupSchema.Fresh)] [InlineData(StartupSchema.InitialStorage)]
    public async Task Development_startup_applies_pending_compiled_migrations_before_authenticated_readiness(StartupSchema state)
    {
        await using var sql = await MigrationStartupFixture.CreateAsync(state);
        if (state == StartupSchema.InitialStorage) await SeedTicketAsync(sql);
        var before = state == StartupSchema.InitialStorage ? await DataSnapshotAsync(sql) : null;
        var sources = MigrationStartupFixture.SourceFingerprints();
        using var jwt = new TestJwtFactory(); await using var api = new MigrationStartupApiFactory(sql, jwt);
        using var client = api.CreateClient(); // Normal Program startup must finish migrations before returning.
        AssertSourcesUnchanged(sources);
        Assert.Equal(ExpectedMigrations, await sql.AppliedMigrationsAsync());
        Assert.Equal(1, await sql.ObjectExistsAsync("dbo.TicketSubjectContainsLiteral"));
        Assert.Contains(api.Probe.Commands, MigrationStartupProbe.IsMigrationWrite);
        Assert.NotEmpty(api.Probe.Contexts); AssertDisposed(api.Probe);
        await AssertReadinessAsync(client, jwt);
        if (before is not null) Assert.Equal(before, await DataSnapshotAsync(sql));
        else
        {
            await using var context = sql.OpenContext();
            Assert.Equal(1, await context.Categories.CountAsync()); Assert.Empty(await context.Agents.ToListAsync());
            Assert.Empty(await context.Tickets.ToListAsync()); Assert.Empty(await context.History.ToListAsync());
        }
        AssertSourcesUnchanged(sources);
    }

    [Fact]
    public async Task Repeated_startup_does_not_reexecute_applied_migrations_or_change_saved_data()
    {
        await using var sql = await MigrationStartupFixture.CreateAsync(StartupSchema.Current);
        await SeedTicketAsync(sql); var before = await DataSnapshotAsync(sql);
        var sources = MigrationStartupFixture.SourceFingerprints(); using var jwt = new TestJwtFactory();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var api = new MigrationStartupApiFactory(sql, jwt); using var client = api.CreateClient();
            Assert.DoesNotContain(api.Probe.Commands, MigrationStartupProbe.IsMigrationWrite);
            await AssertReadinessAsync(client, jwt);
            // Signature validation is real; the fixture never installs an always-successful scheme.
            client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token(expired: true));
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/ticket-reference-data/categories")).StatusCode);
            using var wrongSigner = new TestJwtFactory(); client.DefaultRequestHeaders.Authorization = new("Bearer", wrongSigner.Token());
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/ticket-reference-data/categories")).StatusCode);
            Assert.Equal(ExpectedMigrations, await sql.AppliedMigrationsAsync()); Assert.Equal(before, await DataSnapshotAsync(sql));
            AssertSourcesUnchanged(sources);
        }
    }

    [Theory]
    [InlineData(StartupFault.MigrationCommand)] [InlineData(StartupFault.Cancellation)]
    [InlineData(StartupFault.MissingMigrations)] [InlineData(StartupFault.ModelDrift)]
    public async Task Migration_failure_or_invalid_artifacts_prevent_Development_HTTP_startup(StartupFault fault)
    {
        await using var sql = await MigrationStartupFixture.CreateAsync(StartupSchema.Fresh);
        var sources = MigrationStartupFixture.SourceFingerprints(); using var jwt = new TestJwtFactory();
        await using var api = new MigrationStartupApiFactory(sql, jwt, fault);
        var previous = Console.Error; using var diagnostics = new StringWriter(); Exception? failure;
        try
        {
            Console.SetError(diagnostics);
            failure = Record.Exception(() => { using var unexpectedHealthyClient = api.CreateClient(); });
        }
        finally { Console.SetError(previous); }
        AssertSourcesUnchanged(sources);
        Assert.NotNull(failure); // A responding host against an unmigrated schema is never success.
        Assert.DoesNotContain(MigrationStartupProbe.PrivateMarker, diagnostics.ToString());
        Assert.DoesNotContain(sql.Connection, diagnostics.ToString());
        Assert.False(string.IsNullOrWhiteSpace(diagnostics.ToString()));
        Assert.Equal(0, await sql.ObjectExistsAsync("dbo.Tickets")); Assert.Empty(await sql.AppliedMigrationsAsync());
        if (fault is StartupFault.MigrationCommand or StartupFault.Cancellation)
        { Assert.NotEmpty(api.Probe.Contexts); AssertDisposed(api.Probe); }
        if (fault == StartupFault.Cancellation) Assert.True(api.Probe.CancellationObserved);
    }

    [Fact]
    public void Application_source_has_no_EnsureCreated_call_or_disabled_pending_model_check()
    {
        foreach (var path in MigrationStartupFixture.SourceFingerprints().Keys.Where(path => Path.GetExtension(path) == ".cs"))
        {
            var source = File.ReadAllText(path);
            Assert.False(Regex.IsMatch(source, @"\.\s*EnsureCreated(?:Async)?\s*\("), path);
            Assert.False(Regex.IsMatch(source, @"(?:Ignore|Log)\s*\(\s*(?:RelationalEventId\.)?PendingModelChangesWarning"), path);
        }
    }

    private static async Task AssertReadinessAsync(HttpClient client, TestJwtFactory jwt)
    {
        client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token());
        var response = await client.GetAsync("/api/ticket-reference-data/categories"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var categories = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Contains(categories.EnumerateArray(), category => category.GetProperty("id").GetString() == "00000000-0000-0000-0000-000000000001" && category.GetProperty("displayName").GetString() == "Uncategorised");
    }
    private static void AssertDisposed(MigrationStartupProbe probe)
    { foreach (var context in probe.Contexts.Keys) Assert.Throws<ObjectDisposedException>(() => context.ChangeTracker.Entries().ToArray()); }
    private static void AssertSourcesUnchanged(Dictionary<string, string> before) =>
        Assert.Equal(before.OrderBy(item => item.Key, StringComparer.Ordinal), MigrationStartupFixture.SourceFingerprints().OrderBy(item => item.Key, StringComparer.Ordinal));
    private static async Task SeedTicketAsync(MigrationStartupFixture sql)
    {
        await using var context = sql.OpenContext();
        var ticket = Ticket.Create("Existing migration-startup ticket", "Saved Unicode وصف", new(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Uncategorised"),
            TicketPriority.Normal, new("https://example.test", "fixture-staff", "Fixture Staff"), new(2026, 10, 9, 0, 0, 0, TimeSpan.Zero));
        context.Tickets.Add(ticket); context.History.AddRange(ticket.PendingHistory); await context.SaveChangesAsync();
    }
    private static async Task<string> DataSnapshotAsync(MigrationStartupFixture sql)
    {
        await using var context = sql.OpenContext();
        var tickets = await context.Tickets.AsNoTracking().OrderBy(ticket => ticket.Reference).Select(ticket => new
        { ticket.Reference, ticket.Subject, ticket.Description, ticket.CategoryId, ticket.Priority, ticket.Status, ticket.AssignedAgentId, ticket.CreatedAtUtc, ticket.UpdatedAtUtc, ticket.Revision, Version = EF.Property<byte[]>(ticket, "RowVersion") }).ToListAsync();
        var histories = await context.History.AsNoTracking().OrderBy(entry => entry.Id).ToListAsync();
        var categories = await context.Categories.AsNoTracking().OrderBy(category => category.Id).ToListAsync();
        var agents = await context.Agents.AsNoTracking().OrderBy(agent => agent.Id).ToListAsync();
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { tickets, histories, categories, agents })));
    }
}
