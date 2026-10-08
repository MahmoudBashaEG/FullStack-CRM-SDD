using System.Net;
using System.Text.Json;
using Crm.Domain.Tickets;
using Crm.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
namespace Crm.IntegrationTests.Tickets;

[Collection("SQL")]
public sealed class ListTicketsTests(SqlServerFixture sql)
{
    private static string Category => "30000000-0000-0000-0000-000000000002";
    private static string Agent => "20000000-0000-0000-0000-000000000002";
    private async Task<List<Ticket>> PrepareAsync()
    {
        await using var db = sql.OpenContext();
        var script = await File.ReadAllTextAsync(Path.Combine(ReferenceProvisioningTests.RepositoryRoot(), "tests/backend/Crm.IntegrationTests/Fixtures/TrackingTickets.sql"));
        await db.Database.ExecuteSqlRawAsync(script);
        return await db.Tickets.AsNoTracking().ToListAsync();
    }
    [Theory]
    [InlineData("TrackingSeed", null, null, null, null)]
    [InlineData(" ALPHA ", null, null, null, null)] [InlineData("مشكلة", null, null, null, null)]
    [InlineData("%", null, null, null, null)] [InlineData("_", null, null, null, null)]
    [InlineData("[", null, null, null, null)] [InlineData("]", null, null, null, null)] [InlineData("\\", null, null, null, null)]
    [InlineData("CAFÉ", null, null, null, null)] [InlineData("cafe", null, null, null, null)]
    [InlineData("' OR 1=1--", null, null, null, null)]
    [InlineData("TKT-10000000000000000000000000000001", null, null, null, null)]
    [InlineData("tkt-10000000000000000000000000000001", null, null, null, null)]
    [InlineData("TKT-100000000000", null, null, null, null)]
    [InlineData("TrackingSeed", "Open", null, null, null)] [InlineData("TrackingSeed", "InProgress", null, null, null)]
    [InlineData("TrackingSeed", "Resolved", null, null, null)] [InlineData("TrackingSeed", "Closed", null, null, null)]
    [InlineData("TrackingSeed", null, "30000000-0000-0000-0000-000000000002", null, null)]
    [InlineData("TrackingSeed", null, null, "High", null)]
    [InlineData("TrackingSeed", null, null, null, "unassigned")]
    [InlineData("TrackingSeed", null, null, null, "20000000-0000-0000-0000-000000000002")]
    [InlineData("alpha", "Open", "30000000-0000-0000-0000-000000000001", "Normal", "20000000-0000-0000-0000-000000000001")]
    [InlineData("TrackingSeed", null, "39999999-0000-0000-0000-000000000001", null, null)]
    [InlineData("TrackingSeed", null, null, null, "29999999-0000-0000-0000-000000000001")]
    [InlineData("   ", null, null, null, null)]
    public async Task Search_and_every_filter_match_exact_expected_records(string q, string? status, string? category, string? priority, string? assignment)
    {
        var rows = await PrepareAsync(); var term = q.Trim();
        var expected = rows.Where(t => (term.Length == 0 || t.Reference == term || t.Subject.Contains(term, StringComparison.OrdinalIgnoreCase)) &&
            (status is null || t.Status.ToString() == status) && (category is null || t.CategoryId == Guid.Parse(category)) &&
            (priority is null || t.Priority.ToString() == priority) && (assignment is null || (assignment == "unassigned" ? t.AssignedAgentId is null : t.AssignedAgentId == Guid.Parse(assignment))))
            .OrderByDescending(t => t.UpdatedAtUtc).ThenBy(t => t.Reference, StringComparer.Ordinal).Select(t => t.Reference).ToArray();
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token());
        var pairs = new Dictionary<string, string?> { ["q"] = q, ["status"] = status, ["categoryId"] = category, ["priority"] = priority, ["assignment"] = assignment, ["pageSize"] = "100" };
        var query = string.Join('&', pairs.Where(x => x.Value is not null).Select(x => $"{x.Key}={Uri.EscapeDataString(x.Value!)}"));
        var response = await client.GetAsync("/api/tickets?" + query); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(expected, page.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("reference").GetString()).ToArray());
        Assert.False(page.GetProperty("hasMore").GetBoolean()); Assert.Equal(100, page.GetProperty("pageSize").GetInt32());
        foreach (var item in page.GetProperty("items").EnumerateArray())
        {
            Assert.False(item.TryGetProperty("description", out _)); Assert.False(item.TryGetProperty("createdAtUtc", out _)); Assert.False(item.TryGetProperty("editVersion", out _));
            Assert.True(item.TryGetProperty("category", out _)); Assert.True(item.TryGetProperty("assignedAgent", out _)); Assert.True(item.TryGetProperty("updatedAtUtc", out _));
        }
    }
    [Fact]
    public async Task Stable_order_paging_empty_and_inactive_references_do_not_mutate_records()
    {
        var all = (await PrepareAsync()).Where(t => t.Subject.StartsWith("TrackingSeed", StringComparison.Ordinal)).OrderByDescending(t => t.UpdatedAtUtc).ThenBy(t => t.Reference, StringComparer.Ordinal).ToArray();
        await using var db = sql.OpenContext(); var versions = await db.Tickets.Select(t => new { t.Reference, Version = EF.Property<byte[]>(t, "RowVersion") }).ToListAsync(); var histories = await db.History.CountAsync();
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token());
        var first = JsonDocument.Parse(await client.GetStringAsync("/api/tickets?q=TrackingSeed")).RootElement;
        Assert.Equal(all.Take(25).Select(t => t.Reference), first.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("reference").GetString())); Assert.True(first.GetProperty("hasMore").GetBoolean());
        var second = JsonDocument.Parse(await client.GetStringAsync("/api/tickets?q=TrackingSeed&page=2")).RootElement;
        Assert.Equal(all.Skip(25).Select(t => t.Reference), second.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("reference").GetString())); Assert.False(second.GetProperty("hasMore").GetBoolean());
        var beyond = JsonDocument.Parse(await client.GetStringAsync("/api/tickets?q=TrackingSeed&page=2147483647&pageSize=100")).RootElement;
        Assert.Empty(beyond.GetProperty("items").EnumerateArray()); Assert.False(beyond.GetProperty("hasMore").GetBoolean());
        var inactive = JsonDocument.Parse(await client.GetStringAsync($"/api/tickets?q=TrackingSeed&categoryId={Category}&assignment={Agent}&pageSize=100")).RootElement;
        Assert.NotEmpty(inactive.GetProperty("items").EnumerateArray());
        foreach (var item in inactive.GetProperty("items").EnumerateArray()) { Assert.False(item.GetProperty("category").GetProperty("isAvailable").GetBoolean()); Assert.False(item.GetProperty("assignedAgent").GetProperty("isEligible").GetBoolean()); }
        var after = await db.Tickets.Select(t => new { t.Reference, Version = EF.Property<byte[]>(t, "RowVersion") }).ToListAsync();
        Assert.Equal(histories, await db.History.CountAsync()); foreach (var row in versions) Assert.Equal(row.Version, after.Single(t => t.Reference == row.Reference).Version);
    }
    [Theory]
    [InlineData("status=open")] [InlineData("status=Open,Closed")] [InlineData("status=")]
    [InlineData("priority=0")] [InlineData("categoryId=bad")] [InlineData("categoryId=")] [InlineData("assignment=all")]
    [InlineData("page=0")] [InlineData("pageSize=101")] [InlineData("page=bad")] [InlineData("pageSize=")]
    [InlineData("q=a&q=b")] [InlineData("status=Open&status=Closed")] [InlineData("page=1&page=2")] [InlineData("unknown=1")]
    public async Task Invalid_or_duplicate_query_values_are_400(string query)
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token());
        var response = await client.GetAsync("/api/tickets?" + query); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement; Assert.Equal("validation_failed", error.GetProperty("code").GetString());
    }
    [Fact]
    public async Task Queue_authorization_and_dependency_failures_are_distinct_from_empty_results()
    {
        await PrepareAsync(); using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tickets")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token(allowed: false)); Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/tickets")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token());
        await using var db = sql.OpenContext(); await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_rename N'dbo.Tickets', N'CrmTestTicketsUnavailable'");
        try { var response = await client.GetAsync("/api/tickets"); Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode); Assert.DoesNotContain("CrmTestTicketsUnavailable", await response.Content.ReadAsStringAsync()); }
        finally { await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_rename N'dbo.CrmTestTicketsUnavailable', N'Tickets'"); }
    }
}
