using System.Net;
using System.Net.Http.Json;
using Crm.Application.Accounts.Contracts;
using Crm.IntegrationTests.Fixtures;
namespace Crm.IntegrationTests.Accounts;
internal static class AgentTestSupport
{
    public const string Password="  عربي password  ";
    public static async Task<AgentAccountDto> CreateAsync(AuthenticationApiFixture api,string admin,string name="Agent",string? email=null,string password=Password)
    {
        using var response=await api.SendAsync("/api/agents",HttpMethod.Post,new{displayName=name,email=email??Guid.NewGuid().ToString("N")+"@fixture.test",password},token:admin);
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);return (await response.Content.ReadFromJsonAsync<AgentAccountDto>())!;
    }
}
