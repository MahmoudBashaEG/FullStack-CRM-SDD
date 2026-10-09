using Crm.Infrastructure.Persistence;
using Crm.IntegrationTests.Fixtures;
using Microsoft.Data.SqlClient;
namespace Crm.IntegrationTests.Identity;

[Collection("SQL")]
public sealed class AccountSchemaTests(SqlServerFixture sql)
{
    [Fact] public void Identity_model_stays_in_infrastructure_and_has_a_single_role()
    {
        var type = typeof(CrmDbContext).Assembly.GetType("Crm.Infrastructure.Identity.LocalUser");
        Assert.NotNull(type); Assert.NotNull(type.GetProperty("AccountRole"));
        Assert.Equal(typeof(Guid),type.GetProperty("Id")!.PropertyType);
    }
    [Theory] [InlineData("CrmUsers")] [InlineData("AuthenticationSessions")] [InlineData("RefreshCredentials")] [InlineData("InitialAdminSetup")]
    public async Task Required_schema_is_versioned(string table)
    {
        using var connection = new SqlConnection(sql.Connection); await connection.OpenAsync();
        using var command = new SqlCommand("SELECT COUNT(*) FROM sys.tables WHERE name=@table", connection); command.Parameters.AddWithValue("@table",table);
        Assert.Equal(1,Convert.ToInt32(await command.ExecuteScalarAsync()));
    }
    [Fact] public async Task Required_unique_and_check_constraints_exist()
    {
        using var connection = new SqlConnection(sql.Connection); await connection.OpenAsync();
        using var command = new SqlCommand("SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.CrmUsers') AND is_unique=1 AND name='IX_CrmUsers_NormalizedEmail'",connection);
        Assert.Equal(1,Convert.ToInt32(await command.ExecuteScalarAsync()));
        command.CommandText="SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.CrmUsers') AND name='CK_CrmUsers_AccountRole'";
        Assert.Equal(1,Convert.ToInt32(await command.ExecuteScalarAsync()));
    }
}
