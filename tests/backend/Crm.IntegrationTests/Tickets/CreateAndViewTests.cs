using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Crm.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
namespace Crm.IntegrationTests.Tickets;

[Collection("SQL")]
public sealed class CreateAndViewTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Every_priority_is_accepted_with_a_unique_stable_reference()
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token());
        var references = new HashSet<string>();
        foreach (var priority in new[] { "Low", "Normal", "High", "Urgent" })
        {
            var response = await client.PostAsJsonAsync("/api/tickets", new { subject = "Subject", description = "Description", priority });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var detail = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            var reference = detail.GetProperty("reference").GetString()!; Assert.True(references.Add(reference));
            var fetched = JsonDocument.Parse(await client.GetStringAsync($"/api/tickets/{reference}")).RootElement;
            Assert.Equal(reference, fetched.GetProperty("reference").GetString()); Assert.Equal(priority, fetched.GetProperty("priority").GetString());
        }
    }
    [Fact]
    public async Task Authorized_create_read_and_history_match_atomic_saved_defaults_and_snapshot()
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token());
        var response = await client.PostAsJsonAsync("/api/tickets", new { subject = "  مشكلة <script>  ", description = " وصف\n  details " });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); Assert.Equal("no-store", response.Headers.CacheControl!.ToString()); Assert.Null(response.Headers.ETag);
        var detail = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var reference = detail.GetProperty("reference").GetString()!;
        Assert.EndsWith("/api/tickets/" + reference, response.Headers.Location!.ToString());
        Assert.Equal("Open", detail.GetProperty("status").GetString()); Assert.Equal("Normal", detail.GetProperty("priority").GetString());
        Assert.Equal("Uncategorised", detail.GetProperty("category").GetProperty("displayName").GetString()); Assert.Equal(JsonValueKind.Null, detail.GetProperty("assignedAgent").ValueKind);
        Assert.Equal(8, Convert.FromBase64String(detail.GetProperty("editVersion").GetString()!).Length);
        client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token("staff-b"));
        var get = await client.GetAsync("/api/tickets/" + reference); Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var history = JsonDocument.Parse(await client.GetStringAsync($"/api/tickets/{reference}/history")).RootElement;
        var entry = Assert.Single(history.GetProperty("items").EnumerateArray());
        Assert.Equal("Created", entry.GetProperty("kind").GetString()); Assert.Equal("1", entry.GetProperty("revision").GetString()); Assert.Equal(1, entry.GetProperty("sequence").GetInt32());
        Assert.Equal(jwt.UserId().ToString("D"), entry.GetProperty("actor").GetProperty("subject").GetString());
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("previousValue").ValueKind);
        Assert.Equal("مشكلة <script>", entry.GetProperty("newValue").GetProperty("subject").GetString());
        Assert.Equal(reference, entry.GetProperty("newValue").GetProperty("reference").GetString());
    }
    [Theory]
    [InlineData("{\"subject\":\" \",\"description\":\"D\"}", 400)]
    [InlineData("{\"subject\":\"S\",\"description\":\"D\",\"priority\":null}", 400)]
    [InlineData("{\"subject\":\"S\",\"description\":\"D\",\"categoryId\":null}", 400)]
    [InlineData("{\"subject\":\"S\",\"subject\":\"Other\",\"description\":\"D\"}", 400)]
    [InlineData("{\"subject\":\"S\",\"description\":\"D\",\"actor\":\"forged\"}", 400)]
    [InlineData("{\"subject\":\"S\",\"description\":\"D\",\"categoryId\":\"39999999-0000-0000-0000-000000000001\"}", 409)]
    public async Task Invalid_requests_do_not_persist_partial_tickets_or_history(string payload, int expected)
    {
        await using var before = sql.OpenContext(); var tickets = await before.Tickets.CountAsync(); var histories = await before.History.CountAsync();
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token());
        var response = await client.PostAsync("/api/tickets", new StringContent(payload, Encoding.UTF8, "application/json"));
        Assert.Equal(expected, (int)response.StatusCode); Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement; Assert.True(error.TryGetProperty("code", out _)); Assert.True(error.TryGetProperty("traceId", out _));
        Assert.Equal(tickets, await before.Tickets.CountAsync()); Assert.Equal(histories, await before.History.CountAsync());
    }
    [Fact]
    public async Task Every_current_route_requires_real_signed_queue_authorization()
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient();
        foreach (var route in new[] { "/api/ticket-reference-data/categories", "/api/ticket-reference-data/agents", "/api/tickets/missing", "/api/tickets/missing/history" })
        {
            client.DefaultRequestHeaders.Authorization = null; Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(route)).StatusCode);
            client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token(allowed: false)); Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(route)).StatusCode);
            client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token(issuer: "https://untrusted.test")); Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(route)).StatusCode);
            client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token(expired: true)); Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(route)).StatusCode);
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/tickets/missing")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.DeleteAsync("/api/tickets/missing/history")).StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await client.PostAsync("/api/tickets", new StringContent("text"))).StatusCode);
    }
    [Fact]
    public async Task Persistence_failure_rolls_back_ticket_and_history_together()
    {
        await using var database = sql.OpenContext(); var ticketCount = await database.Tickets.CountAsync(); var historyCount = await database.History.CountAsync();
        await database.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.CrmTestRejectHistory ON dbo.TicketHistoryEntries INSTEAD OF INSERT AS THROW 50002, 'Synthetic history failure', 1;");
        try
        {
            using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token());
            var response = await client.PostAsJsonAsync("/api/tickets", new { subject = "Must roll back", description = "Failed history save" });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal(ticketCount, await database.Tickets.CountAsync()); Assert.Equal(historyCount, await database.History.CountAsync());
            Assert.DoesNotContain("Synthetic history failure", await response.Content.ReadAsStringAsync());
        }
        finally { await database.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.CrmTestRejectHistory"); }
    }
}
