using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Crm.Application.Common;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Tickets;
using Crm.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
namespace Crm.IntegrationTests.Tickets;

[Collection("SQL")]
public sealed class TicketStatusTests(SqlServerFixture sql)
{
    private static async Task<JsonElement> Body(HttpResponseMessage response) => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    private static async Task<JsonElement> Create(HttpClient client) => await Body(await client.PostAsJsonAsync("/api/tickets", new { subject = "Lifecycle issue", description = "تفاصيل محفوظة" }));
    private static string Reference(JsonElement detail) => detail.GetProperty("reference").GetString()!;
    private static string Version(JsonElement detail) => detail.GetProperty("editVersion").GetString()!;
    private static Task<HttpResponseMessage> Change(HttpClient client, JsonElement detail, string status) => client.PutAsJsonAsync($"/api/tickets/{Reference(detail)}/status", new { expectedVersion = Version(detail), status });
    private static Task<HttpResponseMessage> History(HttpClient client, JsonElement detail) => client.GetAsync($"/api/tickets/{Reference(detail)}/history");
    private async Task<JsonElement> Prepared(HttpClient client, string status)
    {
        var detail = await Create(client); if (status == "Open") return detail;
        await using var database = sql.OpenContext(); var reference = Reference(detail);
        // Independent state fixture; avoids relying on transitions under test to arrange the matrix.
        await database.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.Tickets SET Status={status} WHERE Reference={reference}");
        return await Body(await client.GetAsync("/api/tickets/" + reference));
    }
    public static IEnumerable<object[]> StatePairs()
    {
        var states = new[] { "Open", "InProgress", "Resolved", "Closed" };
        var allowed = new HashSet<(string, string)> { ("Open", "InProgress"), ("Open", "Resolved"), ("InProgress", "Resolved"), ("Resolved", "Closed"), ("Resolved", "Open"), ("Closed", "Open") };
        foreach (var from in states) foreach (var to in states) yield return [from, to, allowed.Contains((from, to))];
    }
    [Theory, MemberData(nameof(StatePairs))]
    public async Task Every_state_pair_obeys_the_contract_and_produces_only_actual_readable_history(string from, string to, bool allowed)
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token());
        var original = await Prepared(client, from); var beforeHistory = await Body(await History(client, original));
        var response = await Change(client, original, to); var changed = from != to && allowed;
        Assert.Equal(from == to || allowed ? HttpStatusCode.OK : HttpStatusCode.Conflict, response.StatusCode);
        Assert.Null(response.Headers.ETag); Assert.Null(response.Content.Headers.LastModified); Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        var result = await Body(response); var saved = await Body(await client.GetAsync("/api/tickets/" + Reference(original))); var history = await Body(await History(client, original));
        if (changed)
        {
            Assert.Equal(result.GetRawText(), saved.GetRawText()); Assert.Equal(to, saved.GetProperty("status").GetString()); Assert.NotEqual(Version(original), Version(saved));
            var entries = history.GetProperty("items"); Assert.Equal(2, entries.GetArrayLength()); Assert.Equal(beforeHistory.GetProperty("items")[0].GetRawText(), entries[0].GetRawText());
            var entry = entries[1]; Assert.Equal("StatusChanged", entry.GetProperty("kind").GetString()); Assert.Equal("status", entry.GetProperty("field").GetString()); Assert.Equal("2", entry.GetProperty("revision").GetString()); Assert.Equal(1, entry.GetProperty("sequence").GetInt32());
            Assert.Equal(from, entry.GetProperty("previousValue").GetString()); Assert.Equal(to, entry.GetProperty("newValue").GetString());
            Assert.Equal("https://example.test", entry.GetProperty("actor").GetProperty("issuer").GetString()); Assert.Equal("staff-a", entry.GetProperty("actor").GetProperty("subject").GetString()); Assert.Equal("Test Staff", entry.GetProperty("actor").GetProperty("displayName").GetString());
            Assert.Equal(saved.GetProperty("updatedAtUtc").GetString(), entry.GetProperty("occurredAtUtc").GetString()); Assert.True(Guid.TryParse(entry.GetProperty("id").GetString(), out _));
        }
        else { Assert.Equal(original.GetRawText(), saved.GetRawText()); Assert.Equal(beforeHistory.GetRawText(), history.GetRawText()); if (from != to) Assert.Equal("invalid_transition", result.GetProperty("code").GetString()); }
        foreach (var field in new[] { "reference", "subject", "description", "category", "priority", "assignedAgent", "createdAtUtc" }) Assert.Equal(original.GetProperty(field).GetRawText(), saved.GetProperty(field).GetRawText());
    }
    [Theory]
    [InlineData("{}")] [InlineData("{\"expectedVersion\":null,\"status\":\"Resolved\"}")] [InlineData("{\"expectedVersion\":17,\"status\":\"Resolved\"}")] [InlineData("{\"expectedVersion\":\"AA==\",\"status\":\"Resolved\"}")] [InlineData("{\"expectedVersion\":\"AAAAAAAAAAE= \",\"status\":\"Resolved\"}")]
    public async Task Bad_version_returns_invalid_version_before_existence_check(string payload)
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token());
        var response = await client.PutAsync("/api/tickets/missing/status", new StringContent(payload, Encoding.UTF8, "application/json")); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); var error = await Body(response); Assert.Equal("invalid_version", error.GetProperty("code").GetString()); Assert.True(error.GetProperty("errors").TryGetProperty("expectedVersion", out _));
    }
    [Theory]
    [InlineData("")] [InlineData(",\"status\":null")] [InlineData(",\"status\":17")] [InlineData(",\"status\":\"resolved\"")] [InlineData(",\"status\":\"99\"")] [InlineData(",\"status\":\"Escalated\"")] [InlineData(",\"status\":\"Resolved\",\"agentId\":null")] [InlineData(",\"status\":\"Resolved\",\"status\":\"Closed\"")]
    public async Task Invalid_status_shape_or_unknown_duplicate_fields_leave_ticket_unchanged(string fields)
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token()); var original = await Create(client);
        var payload = "{\"expectedVersion\":\"" + Version(original) + "\"" + fields + "}";
        var response = await client.PutAsync($"/api/tickets/{Reference(original)}/status", new StringContent(payload, Encoding.UTF8, "application/json")); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(original.GetRawText(), (await Body(await client.GetAsync("/api/tickets/" + Reference(original)))).GetRawText()); Assert.Equal(1, (await Body(await History(client, original))).GetProperty("items").GetArrayLength());
    }
    [Theory]
    [InlineData("staff-a", false, false, "https://example.test", "crm-tickets-tests", 403)]
    [InlineData("staff-a", true, true, "https://example.test", "crm-tickets-tests", 401)]
    [InlineData("staff-a", true, false, "https://untrusted.test", "crm-tickets-tests", 401)]
    [InlineData("staff-a", true, false, "https://example.test", "wrong-audience", 401)]
    public async Task Real_JWT_authorization_precedes_mutation_validation(string subject, bool allowed, bool expired, string issuer, string audience, int expected)
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token(subject, allowed, issuer: issuer, audience: audience, expired: expired));
        var response = await client.PutAsJsonAsync("/api/tickets/missing/status", new { }); Assert.Equal(expected, (int)response.StatusCode); Assert.DoesNotContain("ticket_not_found", await response.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task Missing_auth_missing_ticket_and_unsupported_media_have_consistent_statuses()
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/tickets/missing/status", new { })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token()); Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/tickets/missing/status", new { expectedVersion = "AAAAAAAAAAE=", status = "Resolved" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await client.PutAsync("/api/tickets/missing/status", new StringContent("text"))).StatusCode);
        var response = await client.PutAsync("/api/tickets/missing/status", new StringContent("{\"expectedVersion\":\"AAAAAAAAAAE=\",\"status\":\"Resolved\"}", Encoding.UTF8, "application/merge-patch+json")); Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }
    [Fact]
    public async Task Close_reopen_keeps_inactive_references_and_blocks_even_noop_details_and_assignment()
    {
        var category = Guid.NewGuid(); var agent = Guid.NewGuid();
        string Provision(bool active) => JsonSerializer.Serialize(new { schemaVersion = 1, categories = new[] { new { id = category, displayName = "Lifecycle category", isAvailable = active } }, agents = new[] { new { id = agent, issuer = "https://example.test", subject = agent.ToString(), displayName = "Lifecycle agent", isEligible = active } } });
        Assert.Equal(0, (await ReferenceProvisioningTests.RunCliAsync(sql.Connection, Provision(true))).Exit);
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token());
        var detail = await Body(await client.PostAsJsonAsync("/api/tickets", new { subject = "Preserved issue", description = "Description", categoryId = category, priority = "Urgent" }));
        detail = await Body(await client.PutAsJsonAsync($"/api/tickets/{Reference(detail)}/assignment", new { expectedVersion = Version(detail), agentId = agent })); var before = detail; var initialHistory = (await Body(await History(client, detail))).GetProperty("items").EnumerateArray().Select(x => x.GetRawText()).ToArray();
        Assert.Equal(0, (await ReferenceProvisioningTests.RunCliAsync(sql.Connection, Provision(false))).Exit);
        detail = await Body(await client.GetAsync("/api/tickets/" + Reference(detail))); Assert.Equal(Version(before), Version(detail)); Assert.False(detail.GetProperty("category").GetProperty("isAvailable").GetBoolean()); Assert.False(detail.GetProperty("assignedAgent").GetProperty("isEligible").GetBoolean());
        client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token("staff-b", name: null));
        foreach (var status in new[] { "Resolved", "Closed", "Open" })
        {
            var response = await Change(client, detail, status); Assert.Equal(HttpStatusCode.OK, response.StatusCode); detail = await Body(response);
            Assert.Equal(category.ToString(), detail.GetProperty("category").GetProperty("id").GetString()); Assert.Equal(agent.ToString(), detail.GetProperty("assignedAgent").GetProperty("id").GetString());
            if (status == "Closed")
            {
                var historyBefore = await Body(await History(client, detail));
                var edit = await client.PatchAsJsonAsync($"/api/tickets/{Reference(detail)}/details", new { expectedVersion = Version(detail), subject = detail.GetProperty("subject").GetString() }); Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode); Assert.Equal("ticket_closed", (await Body(edit)).GetProperty("code").GetString());
                var assign = await client.PutAsJsonAsync($"/api/tickets/{Reference(detail)}/assignment", new { expectedVersion = Version(detail), agentId = agent }); Assert.Equal(HttpStatusCode.Conflict, assign.StatusCode); Assert.Equal("ticket_closed", (await Body(assign)).GetProperty("code").GetString());
                Assert.Equal(historyBefore.GetRawText(), (await Body(await History(client, detail))).GetRawText());
            }
        }
        foreach (var field in new[] { "reference", "subject", "description", "priority", "createdAtUtc" }) Assert.Equal(before.GetProperty(field).GetRawText(), detail.GetProperty(field).GetRawText());
        var history = (await Body(await History(client, detail))).GetProperty("items").EnumerateArray().ToArray(); Assert.Equal(5, history.Length); Assert.Equal(initialHistory, history.Take(2).Select(x => x.GetRawText()));
        Assert.All(history.Skip(2), entry => { Assert.Equal("StatusChanged", entry.GetProperty("kind").GetString()); Assert.Equal("staff-b", entry.GetProperty("actor").GetProperty("displayName").GetString()); });
        var edited = await client.PatchAsJsonAsync($"/api/tickets/{Reference(detail)}/details", new { expectedVersion = Version(detail), subject = "Editable after reopen" }); Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
    }
    [Fact]
    public async Task Stale_same_status_and_invalid_transition_fail_before_domain_rules()
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token()); var original = await Create(client); var current = await Body(await Change(client, original, "InProgress"));
        foreach (var status in new[] { "InProgress", "Closed" }) { var response = await Change(client, original, status); Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); Assert.Equal("ticket_changed", (await Body(response)).GetProperty("code").GetString()); }
        Assert.Equal(current.GetRawText(), (await Body(await client.GetAsync("/api/tickets/" + Reference(original)))).GetRawText()); Assert.Equal(2, (await Body(await History(client, original))).GetProperty("items").GetArrayLength());
    }
    [Fact]
    public async Task Competing_lifecycle_writes_have_one_winner_with_consistent_saved_values_and_token()
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var a = api.CreateClient(); using var b = api.CreateClient(); a.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token()); b.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token("staff-b")); var original = await Create(a);
        var results = await Task.WhenAll(Change(a, original, "InProgress"), Change(b, original, "Resolved")); var winner = await Body(Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK)); var loser = await Body(Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict)); Assert.Equal("ticket_changed", loser.GetProperty("code").GetString());
        Assert.Equal(winner.GetRawText(), (await Body(await a.GetAsync("/api/tickets/" + Reference(original)))).GetRawText()); var entries = (await Body(await History(a, original))).GetProperty("items"); Assert.Equal(2, entries.GetArrayLength()); Assert.Equal(winner.GetProperty("status").GetString(), entries[1].GetProperty("newValue").GetString());
    }
    [Fact]
    public async Task Save_failure_rolls_back_status_metadata_and_history_with_sanitized_503()
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token()); var original = await Create(client); var before = await Body(await History(client, original));
        await using var database = sql.OpenContext(); await database.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.CrmTestRejectStatusHistory ON dbo.TicketHistoryEntries INSTEAD OF INSERT AS THROW 50002, 'Synthetic status save failure', 1;");
        try
        {
            var response = await Change(client, original, "Resolved"); Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode); Assert.Equal("service_unavailable", (await Body(response)).GetProperty("code").GetString()); Assert.DoesNotContain("Synthetic", await response.Content.ReadAsStringAsync());
            Assert.Equal(original.GetRawText(), (await Body(await client.GetAsync("/api/tickets/" + Reference(original)))).GetRawText()); Assert.Equal(before.GetRawText(), (await Body(await History(client, original))).GetRawText());
        }
        finally { await database.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.CrmTestRejectStatusHistory"); }
    }
    [Fact]
    public async Task Cancellation_after_status_mutation_rolls_back_without_orphan_activity()
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token()); var original = await Create(client);
        await using var database = sql.OpenContext(); using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new TicketMutationStore(database).MutateAsync(Reference(original), Convert.FromBase64String(Version(original)), (scope, _) => { scope.Ticket.ChangeStatus(TicketStatus.Resolved, new("issuer", "staff", "Staff"), DateTimeOffset.UtcNow); cancellation.Cancel(); return Task.FromResult<Failure?>(null); }, cancellation.Token));
        Assert.Equal(original.GetRawText(), (await Body(await client.GetAsync("/api/tickets/" + Reference(original)))).GetRawText()); Assert.Equal(1, (await Body(await History(client, original))).GetProperty("items").GetArrayLength());
    }
}
