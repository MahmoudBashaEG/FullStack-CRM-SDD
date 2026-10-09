using Crm.Domain.Tickets;
using Crm.Infrastructure;
using Crm.Infrastructure.Persistence;
using Crm.IntegrationTests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Crm.IntegrationTests.Identity;

[Collection("SQL")]
public sealed class IdentityMigrationTests
{
    [Fact] public async Task Existing_tickets_history_and_legacy_ids_survive_startup_migration_and_repeat()
    {
        await using var fixture=await MigrationStartupFixture.CreateAsync(StartupSchema.InitialStorage);
        var legacy=Guid.NewGuid();
        await using(var db=fixture.OpenContext())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.AgentReferences(Id,Issuer,Subject,DisplayName,IsEligible) VALUES ({legacy},N'https://legacy.example',N'legacy-subject',N'Legacy Agent',1)");
            var ticket=Ticket.Create("قديم 😀","Preserved description",new(Guid.Parse("00000000-0000-0000-0000-000000000001"),"Uncategorised"),TicketPriority.Normal,new("https://legacy.example","legacy-actor","Original Actor"),DateTimeOffset.UtcNow);
            ticket.Assign(null,new(legacy,"Legacy Agent"),new("https://legacy.example","legacy-actor","Original Actor"),DateTimeOffset.UtcNow);
            db.Tickets.Add(ticket);db.History.AddRange(ticket.PendingHistory);await db.SaveChangesAsync();
        }
        async Task<string> Snapshot(string table)
        {
            using var connection=new SqlConnection(fixture.Connection);await connection.OpenAsync();
            using var command=new SqlCommand("SELECT * FROM dbo."+table+" ORDER BY Id FOR JSON PATH",connection);return (string)(await command.ExecuteScalarAsync())!;
        }
        var tickets=await Snapshot("Tickets");var history=await Snapshot("TicketHistoryEntries");
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ConnectionStrings:Crm"]=fixture.Connection}).Build();
        using var provider=new ServiceCollection().AddLogging().AddInfrastructure(config).BuildServiceProvider();
        await CrmDatabaseMigration.ApplyAsync(provider,CancellationToken.None);
        await using(var db=fixture.OpenContext())
        {
            var reference=await db.Agents.SingleAsync(x=>x.Id==legacy);Assert.Null(reference.LocalUserId);Assert.False(reference.IsEligible);
            Assert.Equal("legacy-subject",reference.Subject);Assert.Equal("https://legacy.example",reference.Issuer);Assert.Equal("Legacy Agent",reference.DisplayName);
            Assert.Empty(await db.Users.ToListAsync());Assert.Empty(await db.InitialAdminSetup.ToListAsync());
        }
        Assert.Equal(tickets,await Snapshot("Tickets"));Assert.Equal(history,await Snapshot("TicketHistoryEntries"));
        var applied=await fixture.AppliedMigrationsAsync();Assert.Contains("20261009124742_LocalCrmAuthentication",applied);
        await CrmDatabaseMigration.ApplyAsync(provider,CancellationToken.None);
        Assert.Equal(applied,await fixture.AppliedMigrationsAsync());Assert.Equal(tickets,await Snapshot("Tickets"));Assert.Equal(history,await Snapshot("TicketHistoryEntries"));
    }
}
