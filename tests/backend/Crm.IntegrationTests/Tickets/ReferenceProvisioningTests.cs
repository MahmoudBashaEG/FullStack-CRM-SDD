using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Application.Tickets.Contracts;
using Crm.Infrastructure.Tickets;
using Crm.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
namespace Crm.IntegrationTests.Tickets;

[Collection("SQL")]
public sealed class ReferenceProvisioningTests(SqlServerFixture sql)
{
    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Crm.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
    public static async Task<(int Exit, string Output)> RunCliAsync(string connection, string input)
    {
        var file = Path.Combine(Path.GetTempPath(), $"crm-input-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(file, input);
        try
        {
            var root = RepositoryRoot();
            var settings = new ProcessStartInfo("dotnet") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            settings.ArgumentList.Add(Path.Combine(root, "src/backend/Crm.Api/bin/Debug/net10.0/Crm.Api.dll"));
            foreach (var argument in new[] { "provision-ticket-references", "--input", file }) settings.ArgumentList.Add(argument);
            settings.Environment["ConnectionStrings__Crm"] = connection;
            // No username/password or alternate process identity; child inherits this Windows account.
            using var process = Process.Start(settings)!;
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(); await error;
            return (process.ExitCode, await output);
        }
        finally { File.Delete(file); }
    }
    [Fact]
    public async Task Upsert_noop_omission_inactivation_and_snapshot_retention_follow_contract()
    {
        var categoryId = Guid.NewGuid(); var agentId = Guid.NewGuid();
        var input = JsonSerializer.Serialize(new { schemaVersion = 1, categories = new[] { new { id = categoryId, displayName = "Original Category", isAvailable = true } }, agents = new[] { new { id = agentId, issuer = "https://example.test", subject = Guid.NewGuid().ToString(), displayName = "Agent", isEligible = true } } });
        var first = await RunCliAsync(sql.Connection, input); Assert.Equal(0, first.Exit);
        var second = await RunCliAsync(sql.Connection, input); Assert.Equal(0, second.Exit); Assert.Contains("\"unchanged\":2", second.Output);
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token());
        var create = await client.PostAsJsonAsync("/api/tickets", new { subject = "Subject", description = "Description", categoryId });
        var saved = JsonDocument.Parse(await create.Content.ReadAsStringAsync()).RootElement;
        var reference = saved.GetProperty("reference").GetString()!; var version = saved.GetProperty("editVersion").GetString();
        var historyBefore = await client.GetStringAsync($"/api/tickets/{reference}/history");
        var change = JsonSerializer.Serialize(new { schemaVersion = 1, categories = new[] { new { id = categoryId, displayName = "Renamed Category", isAvailable = false } } });
        Assert.Equal(0, (await RunCliAsync(sql.Connection, change)).Exit);
        var current = JsonDocument.Parse(await client.GetStringAsync($"/api/tickets/{reference}")).RootElement;
        Assert.Equal(version, current.GetProperty("editVersion").GetString()); Assert.False(current.GetProperty("category").GetProperty("isAvailable").GetBoolean());
        Assert.Equal("Renamed Category", current.GetProperty("category").GetProperty("displayName").GetString());
        Assert.Equal(historyBefore, await client.GetStringAsync($"/api/tickets/{reference}/history"));
        await using var db = sql.OpenContext(); Assert.True((await db.Agents.SingleAsync(x => x.Id == agentId)).IsEligible);
        var rejected = await client.PostAsJsonAsync("/api/tickets", new { subject = "S", description = "D", categoryId }); Assert.Equal(409, (int)rejected.StatusCode);
        var fallback = await client.PostAsJsonAsync("/api/tickets", new { subject = "S", description = "D" }); Assert.Equal(201, (int)fallback.StatusCode);
    }
    [Fact]
    public async Task Identity_rebind_rolls_back_earlier_staged_category_changes()
    {
        var agentId = Guid.NewGuid(); var categoryId = Guid.NewGuid(); var subject = Guid.NewGuid().ToString();
        var initial = JsonSerializer.Serialize(new { schemaVersion = 1, agents = new[] { new { id = agentId, issuer = "https://example.test", subject, displayName = "Agent", isEligible = true } } });
        Assert.Equal(0, (await RunCliAsync(sql.Connection, initial)).Exit);
        var invalid = JsonSerializer.Serialize(new { schemaVersion = 1, categories = new[] { new { id = categoryId, displayName = "Must Roll Back", isAvailable = true } }, agents = new[] { new { id = agentId, issuer = "https://example.test", subject = "changed", displayName = "Agent", isEligible = true } } });
        Assert.Equal(2, (await RunCliAsync(sql.Connection, invalid)).Exit);
        await using var database = sql.OpenContext(); Assert.False(await database.Categories.AnyAsync(x => x.Id == categoryId));
    }
    [Theory]
    [InlineData("{\"schemaVersion\":1,\"delete\":true}")]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1,\"categories\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"categories\":[{\"id\":\"00000000-0000-0000-0000-000000000001\",\"displayName\":\"Changed\",\"isAvailable\":true}]}")]
    [InlineData("{\"schemaVersion\":1,\"categories\":[{\"id\":\"00000000-0000-0000-0000-000000000001\",\"displayName\":\"Uncategorised\",\"isAvailable\":false}]}")]
    [InlineData("{\"schemaVersion\":1,\"categories\":[{\"id\":\"30000000-0000-0000-0000-000000000001\",\"displayName\":\"Missing flag\"}]}")]
    public async Task Strict_provisioning_rejects_unsupported_input_and_preserves_fallback(string input)
    {
        Assert.Equal(2, (await RunCliAsync(sql.Connection, input)).Exit);
        await using var db = sql.OpenContext(); var fallback = await db.Categories.SingleAsync(x => x.Id == TicketDefaults.CategoryId);
        Assert.True(fallback.IsAvailable); Assert.Equal("Uncategorised", fallback.DisplayName);
    }
    [Fact]
    public async Task Database_check_rejects_fallback_deactivation()
    {
        await using var db = sql.OpenContext();
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Database.ExecuteSqlRawAsync("UPDATE dbo.CategoryReferences SET IsAvailable=0 WHERE Id='00000000-0000-0000-0000-000000000001'"));
    }
}
