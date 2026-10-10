using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Infrastructure.Tickets;
using Crm.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
namespace Crm.IntegrationTests.Tickets;

[Collection("SQL")]
public sealed class TicketHistoryTests(SqlServerFixture sql)
{
    public const string Reference = "TKT-60000000000000000000000000000001";
    private static async Task<JsonElement> Body(HttpResponseMessage response) => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    private async Task Seed()
    {
        await using var database = sql.OpenContext(); var path = Path.Combine(ReferenceProvisioningTests.RepositoryRoot(),"tests/backend/Crm.IntegrationTests/Fixtures/FullTicketHistory.sql");
        // JSON braces in this literal fixture must not be interpreted as EF format placeholders.
        await database.Database.OpenConnectionAsync();
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.CommandText = await File.ReadAllTextAsync(path); await command.ExecuteNonQueryAsync();
    }
    private static void Properties(JsonElement json, params string[] names) => Assert.Equal(names.Order(), json.EnumerateObject().Select(x=>x.Name).Order());
    private static void Snapshot(JsonElement value) { Properties(value,"id","displayName"); Assert.True(Guid.TryParse(value.GetProperty("id").GetString(),out _)); Assert.False(string.IsNullOrWhiteSpace(value.GetProperty("displayName").GetString())); }
    [Fact]
    public async Task Complete_prepared_history_has_exact_shapes_numeric_order_and_immutable_snapshots()
    {
        await Seed(); using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql,jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer",jwt.Token());
        var detailBefore = await client.GetStringAsync($"/api/tickets/{Reference}"); var response = await client.GetAsync($"/api/tickets/{Reference}/history?pageSize=100"); Assert.Equal(HttpStatusCode.OK,response.StatusCode); Assert.Null(response.Headers.ETag); Assert.Equal("no-store",response.Headers.CacheControl!.ToString());
        var page = await Body(response); Properties(page,"items","page","pageSize","hasMore"); var entries = page.GetProperty("items").EnumerateArray().ToArray(); Assert.Equal(33,entries.Length); Assert.False(page.GetProperty("hasMore").GetBoolean());
        var revisions = new[] {"1","2","2","2","2","9","10","11","12","13","14","15","16"}.Concat(Enumerable.Range(100,18).Select(x=>x.ToString())).Concat(["9007199254740992","9007199254740993"]);
        Assert.Equal(revisions,entries.Select(x=>x.GetProperty("revision").GetString())); Assert.Equal(new[]{1,2,3,4},entries.Skip(1).Take(4).Select(x=>x.GetProperty("sequence").GetInt32()));
        foreach (var entry in entries)
        {
            Properties(entry,"id","revision","sequence","kind","field","previousValue","newValue","actor","occurredAtUtc"); Assert.True(Guid.TryParse(entry.GetProperty("id").GetString(),out _)); Assert.Equal(JsonValueKind.String,entry.GetProperty("revision").ValueKind); Assert.True(long.Parse(entry.GetProperty("revision").GetString()!)>0); Assert.True(entry.GetProperty("sequence").GetInt32()>0);
            var actor = entry.GetProperty("actor"); Properties(actor,"issuer","subject","displayName"); Assert.Equal("https://example.test",actor.GetProperty("issuer").GetString()); Assert.StartsWith("history-",actor.GetProperty("subject").GetString()); Assert.StartsWith("Historical",actor.GetProperty("displayName").GetString()); Assert.Equal(DateTimeOffset.Parse("2026-10-08T09:00:00Z"),entry.GetProperty("occurredAtUtc").GetDateTimeOffset());
            var before = entry.GetProperty("previousValue"); var after = entry.GetProperty("newValue");
            switch (entry.GetProperty("kind").GetString())
            {
                case "Created": Assert.Equal(JsonValueKind.Null,entry.GetProperty("field").ValueKind); Assert.Equal(JsonValueKind.Null,before.ValueKind); Properties(after,"reference","subject","description","category","priority","status","assignedAgent"); Snapshot(after.GetProperty("category")); Assert.Equal(JsonValueKind.Null,after.GetProperty("assignedAgent").ValueKind); break;
                case "DetailsChanged": if(entry.GetProperty("field").GetString()=="category") { Snapshot(before); Snapshot(after); } else { Assert.Equal(JsonValueKind.String,before.ValueKind); Assert.Equal(JsonValueKind.String,after.ValueKind); } break;
                case "AssignmentChanged": Assert.Equal("assignedAgent",entry.GetProperty("field").GetString()); if(before.ValueKind!=JsonValueKind.Null) Snapshot(before); if(after.ValueKind!=JsonValueKind.Null) Snapshot(after); break;
                case "StatusChanged": Assert.Equal("status",entry.GetProperty("field").GetString()); Assert.Contains(before.GetString(),new[]{"Open","InProgress","Resolved","Closed"}); Assert.Contains(after.GetString(),new[]{"Open","InProgress","Resolved","Closed"}); break;
                default: Assert.Fail("Unexpected history kind."); break;
            }
        }
        Assert.Equal("History category one",entries[0].GetProperty("newValue").GetProperty("category").GetProperty("displayName").GetString()); Assert.Equal("History category two",entries[3].GetProperty("newValue").GetProperty("displayName").GetString()); Assert.Equal("History agent one",entries[5].GetProperty("newValue").GetProperty("displayName").GetString()); Assert.Equal(JsonValueKind.Null,entries[7].GetProperty("newValue").ValueKind);
        Assert.Equal(detailBefore,await client.GetStringAsync($"/api/tickets/{Reference}"));
        await using var database = sql.OpenContext(); var read = await new TicketReadService(database).GetHistoryAsync(Reference,1,100,default); Assert.Equal(33,read!.Items.Count); Assert.Empty(database.ChangeTracker.Entries());
    }
    [Theory]
    [InlineData(1,25,25,true)] [InlineData(2,25,8,false)] [InlineData(3,25,0,false)] [InlineData(1,100,33,false)] [InlineData(2,100,0,false)] [InlineData(int.MaxValue,100,0,false)] [InlineData(2,1,1,true)]
    public async Task Paging_is_bounded_stable_and_distinguishes_empty_from_missing(int page,int size,int count,bool hasMore)
    {
        await Seed(); using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql,jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer",jwt.Token());
        var response = await client.GetAsync($"/api/tickets/{Reference}/history?page={page}&pageSize={size}"); Assert.Equal(HttpStatusCode.OK,response.StatusCode); var result = await Body(response); Assert.Equal(count,result.GetProperty("items").GetArrayLength()); Assert.Equal(hasMore,result.GetProperty("hasMore").GetBoolean()); Assert.Equal(page,result.GetProperty("page").GetInt32()); Assert.Equal(size,result.GetProperty("pageSize").GetInt32());
        var expected = Enumerable.Range(1,33).Skip(page==int.MaxValue?33:(page-1)*size).Take(size).Select(x=>$"70000000-0000-0000-0000-{x:000000000000}"); Assert.Equal(expected,result.GetProperty("items").EnumerateArray().Select(x=>x.GetProperty("id").GetString()));
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/api/tickets/missing/history")).StatusCode);
    }
    [Theory]
    [InlineData("page=0")] [InlineData("page=-1")] [InlineData("pageSize=0")] [InlineData("pageSize=101")] [InlineData("page=1.5")] [InlineData("page=0x10")] [InlineData("page=1&page=2")] [InlineData("pageSize=25&pageSize=25")]
    public async Task Invalid_paging_returns_400(string query)
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql,jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer",jwt.Token()); Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync($"/api/tickets/{Reference}/history?{query}")).StatusCode);
    }
    [Fact]
    public async Task History_requires_authorization_and_has_no_mutation_endpoints()
    {
        await Seed(); using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql,jwt); using var client = api.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync($"/api/tickets/{Reference}/history?page=0")).StatusCode); client.DefaultRequestHeaders.Authorization = new("Bearer",jwt.Token(allowed:false)); Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync($"/api/tickets/{Reference}/history")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer",jwt.Token("staff-b")); var before = await client.GetStringAsync($"/api/tickets/{Reference}/history");
        Assert.Equal(HttpStatusCode.MethodNotAllowed,(await client.PatchAsJsonAsync($"/api/tickets/{Reference}/history",new{ newValue="forged" })).StatusCode); Assert.Equal(HttpStatusCode.MethodNotAllowed,(await client.PutAsJsonAsync($"/api/tickets/{Reference}/history",new{ newValue="forged" })).StatusCode); Assert.Equal(HttpStatusCode.MethodNotAllowed,(await client.DeleteAsync($"/api/tickets/{Reference}/history")).StatusCode); Assert.Equal(before,await client.GetStringAsync($"/api/tickets/{Reference}/history"));
    }
    [Fact]
    public async Task Read_dependency_failure_is_sanitized_503_not_empty_success()
    {
        await Seed(); using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql,jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer",jwt.Token()); await using var database = sql.OpenContext();
        await database.Database.ExecuteSqlRawAsync("EXEC sp_rename N'dbo.TicketHistoryEntries', N'CrmTestHistoryUnavailable'");
        try { var response = await client.GetAsync($"/api/tickets/{Reference}/history"); Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode); Assert.Equal("service_unavailable",(await Body(response)).GetProperty("code").GetString()); Assert.DoesNotContain("TicketHistoryEntries",await response.Content.ReadAsStringAsync()); }
        finally { await database.Database.ExecuteSqlRawAsync("EXEC sp_rename N'dbo.CrmTestHistoryUnavailable', N'TicketHistoryEntries'"); }
    }
    [Fact]
    public async Task Reference_renaming_and_new_reader_identity_do_not_rewrite_any_history_snapshot()
    {
        await Seed(); using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql,jwt); using var client = api.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer",jwt.Token());
        var before = await client.GetStringAsync($"/api/tickets/{Reference}/history?pageSize=100"); var detail = await Body(await client.GetAsync($"/api/tickets/{Reference}"));
        var input = JsonSerializer.Serialize(new { schemaVersion=1, categories=new[] { new { id="40000000-0000-0000-0000-000000000002",displayName="Renamed current category",isAvailable=false } } });
        Assert.Equal(0,(await ReferenceProvisioningTests.RunCliAsync(sql.Connection,input)).Exit); client.DefaultRequestHeaders.Authorization = new("Bearer",jwt.Token("staff-b",name:"Current Reader Name"));
        Assert.Equal(2,(await ReferenceProvisioningTests.RunCliAsync(sql.Connection,"{\"schemaVersion\":1,\"agents\":[{\"id\":\"50000000-0000-0000-0000-000000000001\",\"issuer\":\"https://example.test\",\"subject\":\"history-agent-one\",\"displayName\":\"Forbidden legacy rename\",\"isEligible\":false}]}")).Exit);
        var current = await Body(await client.GetAsync($"/api/tickets/{Reference}")); Assert.Equal("Renamed current category",current.GetProperty("category").GetProperty("displayName").GetString()); Assert.Equal(detail.GetProperty("assignedAgent").GetRawText(),current.GetProperty("assignedAgent").GetRawText()); Assert.Equal(detail.GetProperty("editVersion").GetString(),current.GetProperty("editVersion").GetString()); Assert.Equal(detail.GetProperty("updatedAtUtc").GetString(),current.GetProperty("updatedAtUtc").GetString());
        Assert.Equal(before,await client.GetStringAsync($"/api/tickets/{Reference}/history?pageSize=100"));
    }
    [Fact]
    public async Task Real_commands_emit_a_complete_account_and_failed_noop_operations_add_zero_entries()
    {
        var categoryA=Guid.NewGuid(); var categoryB=Guid.NewGuid(); var agentA=await LocalAgentFixture.CreateAsync(sql.Connection,"Original agent A"); var agentB=await LocalAgentFixture.CreateAsync(sql.Connection,"Original agent B");
        string Provision(bool active) => JsonSerializer.Serialize(new { schemaVersion=1, categories=new[] { new {id=categoryA,displayName=active?"Original category A":"Renamed category A",isAvailable=active},new {id=categoryB,displayName=active?"Original category B":"Renamed category B",isAvailable=active} } });
        Assert.Equal(0,(await ReferenceProvisioningTests.RunCliAsync(sql.Connection,Provision(true))).Exit);
        using var jwt=new TestJwtFactory(); await using var api=new CrmApiFactory(sql,jwt); using var client=api.CreateClient(); client.DefaultRequestHeaders.Authorization=new("Bearer",jwt.Token(name:"Creator Staff"));
        var original=await Body(await client.PostAsJsonAsync("/api/tickets",new{subject="Original subject",description="Original description",categoryId=categoryA})); var detail=original; var reference=detail.GetProperty("reference").GetString();
        string Version() => detail.GetProperty("editVersion").GetString()!;
        async Task Accept(HttpResponseMessage response) { Assert.Equal(HttpStatusCode.OK,response.StatusCode); detail=await Body(response); }
        client.DefaultRequestHeaders.Authorization=new("Bearer",jwt.Token("staff-b",name:"Editor Staff"));
        await Accept(await client.PatchAsJsonAsync($"/api/tickets/{reference}/details",new{expectedVersion=Version(),subject="Changed subject",description="وصف جديد",categoryId=categoryB,priority="Urgent"}));
        foreach(var agent in new Guid?[]{agentA,agentB,null}) await Accept(await client.PutAsJsonAsync($"/api/tickets/{reference}/assignment",new{expectedVersion=Version(),agentId=agent}));
        var transitions=new[]{("Open","InProgress"),("InProgress","Resolved"),("Resolved","Open"),("Open","Resolved"),("Resolved","Closed"),("Closed","Open")};
        foreach(var (_,status) in transitions) await Accept(await client.PutAsJsonAsync($"/api/tickets/{reference}/status",new{expectedVersion=Version(),status}));
        var history=await Body(await client.GetAsync($"/api/tickets/{reference}/history")); var entries=history.GetProperty("items").EnumerateArray().ToArray(); Assert.Equal(14,entries.Length);
        Assert.Equal(new[]{"Created","DetailsChanged","DetailsChanged","DetailsChanged","DetailsChanged","AssignmentChanged","AssignmentChanged","AssignmentChanged","StatusChanged","StatusChanged","StatusChanged","StatusChanged","StatusChanged","StatusChanged"},entries.Select(x=>x.GetProperty("kind").GetString()));
        Assert.Equal("Creator Staff",entries[0].GetProperty("actor").GetProperty("displayName").GetString()); Assert.All(entries.Skip(1),x=>Assert.Equal("Editor Staff",x.GetProperty("actor").GetProperty("displayName").GetString()));
        Assert.Equal(new[]{"subject","description","category","priority"},entries.Skip(1).Take(4).Select(x=>x.GetProperty("field").GetString()));
        Assert.Equal(new[]{"Original subject","Original description"},entries.Skip(1).Take(2).Select(x=>x.GetProperty("previousValue").GetString())); Assert.Equal("Original category A",entries[3].GetProperty("previousValue").GetProperty("displayName").GetString()); Assert.Equal("Original category B",entries[3].GetProperty("newValue").GetProperty("displayName").GetString()); Assert.Equal("Normal",entries[4].GetProperty("previousValue").GetString()); Assert.Equal("Urgent",entries[4].GetProperty("newValue").GetString());
        Assert.Equal(JsonValueKind.Null,entries[5].GetProperty("previousValue").ValueKind); Assert.Equal("Original agent A",entries[5].GetProperty("newValue").GetProperty("displayName").GetString()); Assert.Equal("Original agent A",entries[6].GetProperty("previousValue").GetProperty("displayName").GetString()); Assert.Equal("Original agent B",entries[6].GetProperty("newValue").GetProperty("displayName").GetString()); Assert.Equal(JsonValueKind.Null,entries[7].GetProperty("newValue").ValueKind);
        Assert.Equal(transitions,entries.Skip(8).Select(x=>(x.GetProperty("previousValue").GetString()!,x.GetProperty("newValue").GetString()!)));
        var saved=detail.GetRawText();
        await Accept(await client.PatchAsJsonAsync($"/api/tickets/{reference}/details",new{expectedVersion=Version(),subject="Changed subject"})); await Accept(await client.PutAsJsonAsync($"/api/tickets/{reference}/assignment",new{expectedVersion=Version(),agentId=(Guid?)null})); await Accept(await client.PutAsJsonAsync($"/api/tickets/{reference}/status",new{expectedVersion=Version(),status="Open"})); Assert.Equal(saved,detail.GetRawText());
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PatchAsJsonAsync($"/api/tickets/{reference}/details",new{expectedVersion=Version(),subject=" "})).StatusCode); Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync($"/api/tickets/{reference}/assignment",new{expectedVersion=Version(),agentId=Guid.NewGuid()})).StatusCode); Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync($"/api/tickets/{reference}/status",new{expectedVersion=Version(),status="Closed"})).StatusCode); Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync($"/api/tickets/{reference}/status",new{expectedVersion=original.GetProperty("editVersion").GetString(),status="Open"})).StatusCode);
        Assert.Equal(saved,(await Body(await client.GetAsync($"/api/tickets/{reference}"))).GetRawText()); Assert.Equal(history.GetRawText(),(await Body(await client.GetAsync($"/api/tickets/{reference}/history"))).GetRawText());
        Assert.Equal(0,(await ReferenceProvisioningTests.RunCliAsync(sql.Connection,Provision(false))).Exit); await LocalAgentFixture.UpdateAsync(sql.Connection,agentA,"Renamed agent A",false); await LocalAgentFixture.UpdateAsync(sql.Connection,agentB,"Renamed agent B",false); Assert.Equal(history.GetRawText(),(await Body(await client.GetAsync($"/api/tickets/{reference}/history"))).GetRawText());
    }
    [Theory]
    [InlineData(1,1)] [InlineData(0,1)] [InlineData(1,0)]
    public async Task Database_rejects_duplicate_or_nonpositive_history_order_without_partial_entries(long revision,int sequence)
    {
        await Seed(); await using var database=sql.OpenContext(); var ticketId=Guid.Parse("60000000-0000-0000-0000-000000000001"); var newId=Guid.NewGuid();
        var failure=await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(()=>database.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.TicketHistoryEntries(Id,TicketId,Revision,Sequence,Kind,Field,PreviousValue,NewValue,ActorIssuer,ActorSubject,ActorDisplayName,OccurredAtUtc) SELECT {newId},TicketId,{revision},{sequence},Kind,Field,PreviousValue,NewValue,ActorIssuer,ActorSubject,ActorDisplayName,OccurredAtUtc FROM dbo.TicketHistoryEntries WHERE TicketId={ticketId} AND Revision=1 AND Sequence=1"));
        if(revision==1 && sequence==1) Assert.Contains(failure.Number,new[]{2601,2627}); else Assert.Equal(547,failure.Number);
        Assert.Equal(33,await database.History.CountAsync(x=>x.TicketId==ticketId)); Assert.False(await database.History.AnyAsync(x=>x.Id==newId));
    }
}
