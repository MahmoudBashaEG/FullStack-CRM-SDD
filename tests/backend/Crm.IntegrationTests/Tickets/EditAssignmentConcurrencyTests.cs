using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Crm.IntegrationTests.Fixtures;
using Crm.Application.Common;
using Crm.Infrastructure.Tickets;
using Crm.Infrastructure.Persistence;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
namespace Crm.IntegrationTests.Tickets;

[Collection("SQL")]
public sealed class EditAssignmentConcurrencyTests(SqlServerFixture sql)
{
    private async Task<Guid> Provision(Guid category, Guid agent, bool active, string label = "Snapshot")
    {
        var input = JsonSerializer.Serialize(new { schemaVersion = 1, categories = new[] { new { id = category, displayName = label, isAvailable = active } } });
        Assert.Equal(0, (await ReferenceProvisioningTests.RunCliAsync(sql.Connection, input)).Exit);
        await using var db=sql.OpenContext();if(await db.Agents.AnyAsync(x=>x.Id==agent)){await LocalAgentFixture.UpdateAsync(sql.Connection,agent,label+" agent",active);return agent;}
        return await LocalAgentFixture.CreateAsync(sql.Connection,label+" agent",active);
    }
    private static async Task<JsonElement> Body(HttpResponseMessage response) => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    private static async Task<JsonElement> Create(HttpClient client) => await Body(await client.PostAsJsonAsync("/api/tickets", new { subject = "Edit test", description = "Original description" }));
    private static Task<HttpResponseMessage> Patch(HttpClient client, JsonElement ticket, object payload) => client.PatchAsJsonAsync($"/api/tickets/{ticket.GetProperty("reference").GetString()}/details", payload);
    [Theory]
    [InlineData("{}")] [InlineData("{\"expectedVersion\":null,\"subject\":\"S\"}")] [InlineData("{\"expectedVersion\":\"AA==\",\"subject\":\"S\"}")] [InlineData("{\"expectedVersion\":\"AAAAAAAAAAE= \",\"subject\":\"S\"}")]
    public async Task Invalid_version_is_400_with_field_error(string payload)
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token());
        var response = await client.PatchAsync("/api/tickets/missing/details", new StringContent(payload, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); var error = await Body(response); Assert.Equal("invalid_version", error.GetProperty("code").GetString()); Assert.True(error.GetProperty("errors").TryGetProperty("expectedVersion", out _));
    }
    [Fact]
    public async Task Actual_changes_are_atomic_readable_and_fresh_noops_preserve_token_time()
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token());
        var original = await Create(client); var version = original.GetProperty("editVersion").GetString();
        var response = await Patch(client, original, new { expectedVersion = version, subject = " Changed ", description = " وصف ", priority = "Urgent" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Contains("application/json", response.Headers.GetValues("Accept-Patch")); Assert.Null(response.Headers.ETag);
        var changed = await Body(response); Assert.Equal("Changed", changed.GetProperty("subject").GetString()); Assert.Equal("وصف", changed.GetProperty("description").GetString()); Assert.NotEqual(version, changed.GetProperty("editVersion").GetString());
        var reference = original.GetProperty("reference").GetString();
        var history = await Body(await client.GetAsync($"/api/tickets/{reference}/history")); var entries = history.GetProperty("items").EnumerateArray().ToArray(); Assert.Equal(4, entries.Length);
        Assert.Equal(new[] { "subject", "description", "priority" }, entries.Skip(1).Select(x => x.GetProperty("field").GetString()));
        Assert.All(entries.Skip(1), x => { Assert.Equal("2", x.GetProperty("revision").GetString()); Assert.Equal("DetailsChanged", x.GetProperty("kind").GetString()); Assert.Equal(jwt.UserId().ToString("D"), x.GetProperty("actor").GetProperty("subject").GetString()); Assert.Equal(JsonValueKind.String, x.GetProperty("newValue").ValueKind); });
        var noop = await Patch(client, changed, new { expectedVersion = changed.GetProperty("editVersion").GetString(), subject = " Changed " }); Assert.Equal(HttpStatusCode.OK, noop.StatusCode);
        var same = await Body(noop); Assert.Equal(changed.GetProperty("editVersion").GetString(), same.GetProperty("editVersion").GetString()); Assert.Equal(changed.GetProperty("updatedAtUtc").GetString(), same.GetProperty("updatedAtUtc").GetString());
        var stale = await Patch(client, original, new { expectedVersion = version, subject = "Changed" }); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode); Assert.Equal("ticket_changed", (await Body(stale)).GetProperty("code").GetString());
        Assert.Equal(4, (await Body(await client.GetAsync($"/api/tickets/{reference}/history"))).GetProperty("items").GetArrayLength());
    }
    [Fact]
    public async Task Simultaneous_writers_have_one_winner_and_its_response_matches_saved_token_and_values()
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var a = api.CreateClient(); using var b = api.CreateClient(); a.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token()); b.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token("staff-b"));
        var ticket = await Create(a); var version = ticket.GetProperty("editVersion").GetString();
        var results = await Task.WhenAll(Patch(a, ticket, new { expectedVersion = version, subject = "Writer A" }), Patch(b, ticket, new { expectedVersion = version, subject = "Writer B" }));
        var winner = await Body(Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK)); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        var saved = await Body(await a.GetAsync("/api/tickets/" + ticket.GetProperty("reference").GetString())); Assert.Equal(winner.GetProperty("subject").GetString(), saved.GetProperty("subject").GetString()); Assert.Equal(winner.GetProperty("editVersion").GetString(), saved.GetProperty("editVersion").GetString());
    }
    [Fact]
    public async Task Simultaneous_real_write_and_noop_serialize_without_silent_stale_success()
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token()); var ticket = await Create(client); var version = ticket.GetProperty("editVersion").GetString();
        var results = await Task.WhenAll(Patch(client, ticket, new { expectedVersion = version, subject = "Real change" }), Patch(client, ticket, new { expectedVersion = version, subject = "Edit test" })); Assert.Equal(HttpStatusCode.OK, results[0].StatusCode);
        if (results[1].StatusCode == HttpStatusCode.OK) { var noop = await Body(results[1]); Assert.Equal(version, noop.GetProperty("editVersion").GetString()); Assert.Equal(ticket.GetProperty("updatedAtUtc").GetString(), noop.GetProperty("updatedAtUtc").GetString()); }
        else { Assert.Equal(HttpStatusCode.Conflict, results[1].StatusCode); Assert.Equal("ticket_changed", (await Body(results[1])).GetProperty("code").GetString()); }
        var saved = await Body(await client.GetAsync("/api/tickets/" + ticket.GetProperty("reference").GetString())); Assert.Equal("Real change", saved.GetProperty("subject").GetString()); Assert.NotEqual(version, saved.GetProperty("editVersion").GetString());
        Assert.Equal(2, (await Body(await client.GetAsync($"/api/tickets/{ticket.GetProperty("reference").GetString()}/history"))).GetProperty("items").GetArrayLength());
    }
    [Theory]
    [InlineData("subject", "null")] [InlineData("priority", "\"normal\"")] [InlineData("categoryId", "null")] [InlineData("editVersion", "\"forged\"")] [InlineData("subject", "\"\",\"subject\":\"other\"")]
    public async Task Invalid_patch_never_changes_saved_values(string key, string value)
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token()); var ticket = await Create(client); var reference = ticket.GetProperty("reference").GetString();
        var payload = "{\"expectedVersion\":\"" + ticket.GetProperty("editVersion").GetString() + "\",\"" + key + "\":" + value + "}";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsync($"/api/tickets/{reference}/details", new StringContent(payload, Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(ticket.GetRawText(), (await Body(await client.GetAsync("/api/tickets/" + reference))).GetRawText());
    }
    [Fact]
    public async Task Failed_history_save_rolls_back_details_and_token()
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token()); var ticket = await Create(client);
        await using var database = sql.OpenContext(); await database.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.CrmTestRejectEditHistory ON dbo.TicketHistoryEntries INSTEAD OF INSERT AS THROW 50002, 'Synthetic failure', 1;");
        try { Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Patch(client, ticket, new { expectedVersion = ticket.GetProperty("editVersion").GetString(), subject = "Must rollback" })).StatusCode); Assert.Equal(ticket.GetRawText(), (await Body(await client.GetAsync("/api/tickets/" + ticket.GetProperty("reference").GetString()))).GetRawText()); }
        finally { await database.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.CrmTestRejectEditHistory"); }
    }
    [Fact]
    public async Task Inactivation_keeps_versions_history_and_retained_references_but_rejects_new_selections()
    {
        var category = Guid.NewGuid(); var agent = Guid.NewGuid(); agent=await Provision(category, agent, true);
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token());
        var ticket = await Body(await client.PostAsJsonAsync("/api/tickets", new { subject = "S", description = "D", categoryId = category })); var reference = ticket.GetProperty("reference").GetString();
        async Task<HttpResponseMessage> Assign(JsonElement current, Guid? id) => await client.PutAsJsonAsync($"/api/tickets/{reference}/assignment", new { expectedVersion = current.GetProperty("editVersion").GetString(), agentId = id });
        ticket = await Body(await Assign(ticket, agent)); var version = ticket.GetProperty("editVersion").GetString(); var time = ticket.GetProperty("updatedAtUtc").GetString(); var historyBefore = await client.GetStringAsync($"/api/tickets/{reference}/history");
        await Provision(category, agent, false, "Renamed"); ticket = await Body(await client.GetAsync("/api/tickets/" + reference));
        Assert.Equal(version, ticket.GetProperty("editVersion").GetString()); Assert.Equal(time, ticket.GetProperty("updatedAtUtc").GetString()); Assert.Equal(historyBefore, await client.GetStringAsync($"/api/tickets/{reference}/history"));
        Assert.False(ticket.GetProperty("category").GetProperty("isAvailable").GetBoolean()); Assert.False(ticket.GetProperty("assignedAgent").GetProperty("isEligible").GetBoolean());
        ticket = await Body(await Patch(client, ticket, new { expectedVersion = version, categoryId = category, subject = "Unrelated edit" }));
        var retained = await Assign(ticket, agent); Assert.Equal(HttpStatusCode.OK, retained.StatusCode); Assert.Equal(ticket.GetProperty("editVersion").GetString(), (await Body(retained)).GetProperty("editVersion").GetString());
        ticket = await Body(await Assign(ticket, null)); Assert.Equal(JsonValueKind.Null, ticket.GetProperty("assignedAgent").ValueKind); Assert.Equal("Open", ticket.GetProperty("status").GetString());
        var rejected = await Assign(ticket, agent); Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode); Assert.Equal("agent_ineligible", (await Body(rejected)).GetProperty("code").GetString());
        ticket = await Body(await Patch(client, ticket, new { expectedVersion = ticket.GetProperty("editVersion").GetString(), categoryId = Guid.Parse("00000000-0000-0000-0000-000000000001") }));
        rejected = await Patch(client, ticket, new { expectedVersion = ticket.GetProperty("editVersion").GetString(), categoryId = category, subject = "Must reject atomically" }); Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode); Assert.Equal("category_unavailable", (await Body(rejected)).GetProperty("code").GetString());
        var history = (await Body(await client.GetAsync($"/api/tickets/{reference}/history"))).GetProperty("items").EnumerateArray().ToArray(); Assert.Equal(5, history.Length);
        Assert.Equal("Snapshot agent", history[1].GetProperty("newValue").GetProperty("displayName").GetString()); Assert.Equal(JsonValueKind.Null, history[1].GetProperty("previousValue").ValueKind);
        Assert.Equal("Renamed agent", history[3].GetProperty("previousValue").GetProperty("displayName").GetString()); Assert.Equal(JsonValueKind.Null, history[3].GetProperty("newValue").ValueKind);
        Assert.Equal("Renamed", history[4].GetProperty("previousValue").GetProperty("displayName").GetString());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Mutation_authorization_precedes_existence_and_validation(bool assignment)
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient();
        async Task<HttpResponseMessage> Send() => assignment ? await client.PutAsJsonAsync("/api/tickets/missing/assignment", new { }) : await client.PatchAsJsonAsync("/api/tickets/missing/details", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send()).StatusCode); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token(allowed: false)); Assert.Equal(HttpStatusCode.Forbidden, (await Send()).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token()); Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await client.PatchAsync("/api/tickets/missing/details", new StringContent("text"))).StatusCode);
    }
    [Fact]
    public async Task Missing_assignment_and_empty_patch_are_invalid_and_closed_noops_are_conflicts()
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token()); var ticket = await Create(client); var reference = ticket.GetProperty("reference").GetString();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/tickets/{reference}/assignment", new { expectedVersion = ticket.GetProperty("editVersion").GetString() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Patch(client, ticket, new { expectedVersion = ticket.GetProperty("editVersion").GetString() })).StatusCode);
        // Prepared Closed state; the lifecycle operation belongs to US4 and is not implemented here.
        await using var database = sql.OpenContext(); await database.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.Tickets SET Status=N'Closed' WHERE Reference={reference}"); ticket = await Body(await client.GetAsync("/api/tickets/" + reference));
        var response = await Patch(client, ticket, new { expectedVersion = ticket.GetProperty("editVersion").GetString(), subject = ticket.GetProperty("subject").GetString() }); Assert.Equal("ticket_closed", (await Body(response)).GetProperty("code").GetString());
        response = await client.PutAsJsonAsync($"/api/tickets/{reference}/assignment", new { expectedVersion = ticket.GetProperty("editVersion").GetString(), agentId = (Guid?)null }); Assert.Equal("ticket_closed", (await Body(response)).GetProperty("code").GetString());
    }
    [Fact]
    public async Task Cancellation_after_domain_change_rolls_back_and_releases_locks_without_orphans()
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token()); var ticket = await Create(client); var reference = ticket.GetProperty("reference").GetString()!;
        await using var database = sql.OpenContext(); using var source = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new TicketMutationStore(database).MutateAsync(reference, Convert.FromBase64String(ticket.GetProperty("editVersion").GetString()!), (scope, ct) =>
        {
            scope.Ticket.UpdateDetails("Cancelled", scope.Ticket.Description, new(scope.Category.Id, scope.Category.DisplayName), new(scope.Category.Id, scope.Category.DisplayName), scope.Ticket.Priority, new("issuer", "staff", "Staff"), DateTimeOffset.UtcNow);
            source.Cancel(); return Task.FromResult<Failure?>(null);
        }, source.Token));
        Assert.Equal(ticket.GetRawText(), (await Body(await client.GetAsync("/api/tickets/" + reference))).GetRawText()); Assert.Equal(1, (await Body(await client.GetAsync($"/api/tickets/{reference}/history"))).GetProperty("items").GetArrayLength());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task New_selection_lock_serializes_provisioning_and_keeps_event_label_snapshot(bool assignment)
    {
        var category = Guid.NewGuid(); var agent = Guid.NewGuid(); agent=await Provision(category, agent, true);
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token()); var ticket = await Create(client);
        await using var database = sql.OpenContext(); await using var provisioning = sql.OpenContext(); var selected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var mutation = new TicketMutationStore(database).MutateAsync(ticket.GetProperty("reference").GetString()!, Convert.FromBase64String(ticket.GetProperty("editVersion").GetString()!), async (scope, token) =>
        {
            var selectedCategory = assignment ? null : await scope.SelectCategory(category, token);
            var selectedAgent = assignment ? await scope.SelectAgent(agent, token) : null;
            selected.SetResult(); await release.Task.WaitAsync(token);
            if (assignment) scope.Ticket.Assign(null, new(selectedAgent!.Id, selectedAgent.DisplayName), new("issuer", "staff", "Staff"), DateTimeOffset.UtcNow);
            else scope.Ticket.UpdateDetails(scope.Ticket.Subject, scope.Ticket.Description, new(scope.Category.Id, scope.Category.DisplayName), new(selectedCategory!.Id, selectedCategory.DisplayName), scope.Ticket.Priority, new("issuer", "staff", "Staff"), DateTimeOffset.UtcNow);
            return null;
        }, deadline.Token);
        await selected.Task.WaitAsync(deadline.Token);
        Task provision=assignment?LocalAgentFixture.UpdateAsync(sql.Connection,agent,"Later agent",false,deadline.Token):new ReferenceProvisioner(provisioning).ApplyAsync(new(){SchemaVersion=1,Categories=[new(category,"Later label",false)]},deadline.Token);
        try { await Task.Delay(100, deadline.Token); Assert.False(provision.IsCompleted); } finally { release.TrySetResult(); }
        var committed = await mutation; Assert.True(committed.Succeeded); await provision;
        var current = await Body(await client.GetAsync("/api/tickets/" + ticket.GetProperty("reference").GetString())); Assert.Equal(committed.Value!.EditVersion, current.GetProperty("editVersion").GetString()); Assert.False(assignment ? current.GetProperty("assignedAgent").GetProperty("isEligible").GetBoolean() : current.GetProperty("category").GetProperty("isAvailable").GetBoolean());
        var history = await Body(await client.GetAsync($"/api/tickets/{committed.Value.Reference}/history")); Assert.Equal(assignment ? "Snapshot agent" : "Snapshot", history.GetProperty("items")[1].GetProperty("newValue").GetProperty("displayName").GetString());
    }
    [Fact]
    public async Task Lock_timeout_is_dependency_failure_without_implicit_replay()
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token()); var ticket = await Create(client); var reference = ticket.GetProperty("reference").GetString()!;
        await using var blocker = sql.OpenContext(); await using var transaction = await blocker.Database.BeginTransactionAsync(); await blocker.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.Tickets SET Subject=Subject WHERE Reference={reference}");
        await using var database = sql.OpenContext(); database.Database.SetCommandTimeout(1); Assert.False(database.Database.CreateExecutionStrategy().RetriesOnFailure); var calls = 0;
        await Assert.ThrowsAsync<DependencyUnavailableException>(() => new TicketMutationStore(database).MutateAsync(reference, Convert.FromBase64String(ticket.GetProperty("editVersion").GetString()!), (_, _) => { calls++; return Task.FromResult<Failure?>(null); }, default)); Assert.Equal(0, calls);
        await transaction.RollbackAsync(); Assert.Equal(ticket.GetRawText(), (await Body(await client.GetAsync("/api/tickets/" + reference))).GetRawText());
    }
    [Fact]
    public async Task Real_deadlock_has_one_dependency_failure_and_does_not_replay_either_command()
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token()); var a = await Create(client); var b = await Create(client);
        await using var first = sql.OpenContext(); await using var second = sql.OpenContext();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var arrivals = 0; var calls = 0; using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        async Task<bool> Execute(CrmDbContext context, JsonElement own, JsonElement other)
        {
            try
            {
                var result = await new TicketMutationStore(context).MutateAsync(own.GetProperty("reference").GetString()!, Convert.FromBase64String(own.GetProperty("editVersion").GetString()!), async (scope, token) =>
                {
                    Interlocked.Increment(ref calls); if (Interlocked.Increment(ref arrivals) == 2) ready.SetResult(); await ready.Task.WaitAsync(token);
                    // Opposite ticket locks deliberately create a real SQL deadlock in this fixture only.
                    var otherReference = other.GetProperty("reference").GetString(); await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.Tickets SET Subject=Subject WHERE Reference={otherReference}", token);
                    scope.Ticket.UpdateDetails("Winner", scope.Ticket.Description, new(scope.Category.Id, scope.Category.DisplayName), new(scope.Category.Id, scope.Category.DisplayName), scope.Ticket.Priority, new("issuer", "staff", "Staff"), DateTimeOffset.UtcNow); return null;
                }, deadline.Token); Assert.True(result.Succeeded); return true;
            }
            catch (DependencyUnavailableException) { return false; }
        }
        var outcomes = await Task.WhenAll(Execute(first, a, b), Execute(second, b, a)); Assert.Single(outcomes, x => x); Assert.Single(outcomes, x => !x); Assert.Equal(2, calls);
        var count = 0; foreach (var ticket in new[] { a, b }) count += (await Body(await client.GetAsync($"/api/tickets/{ticket.GetProperty("reference").GetString()}/history"))).GetProperty("items").GetArrayLength(); Assert.Equal(3, count);
    }
}
