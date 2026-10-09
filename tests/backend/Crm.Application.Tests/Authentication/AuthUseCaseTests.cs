using Crm.Application.Authentication.Commands;
using Crm.Application.Authentication.Contracts;
using Crm.Application.Authentication.Ports;
using Crm.Application.Common;
namespace Crm.Application.Tests.Authentication;

public sealed class AuthUseCaseTests
{
    private sealed class Operations : IAuthenticationOperations
    {
        public string? Email,Password,Credential;public TrustedAuthentication? Bearer;public int Calls;
        public Task<OperationResult<SessionIssue>> SignInAsync(string email,string password,string? prior,CancellationToken ct){Email=email;Password=password;Credential=prior;Calls++;return Task.FromResult(OperationResult<SessionIssue>.Fail("invalid_credentials",401));}
        public Task<OperationResult<SessionIssue>> RefreshAsync(string? credential,CancellationToken ct){Credential=credential;Calls++;return Task.FromResult(OperationResult<SessionIssue>.Fail("authentication_invalid",401));}
        public Task<OperationResult<bool>> SignOutAsync(string? credential,TrustedAuthentication? bearer,CancellationToken ct){Credential=credential;Bearer=bearer;Calls++;return Task.FromResult(OperationResult<bool>.Success(true));}
    }
    [Fact] public async Task Signin_normalizes_email_but_preserves_exact_password_and_generic_failure()
    {
        var port=new Operations();var password="  Unicode عربي  ";var result=await new SignInHandler(port).Handle(new(" Admin@fixture.test ",password,"prior"),default);
        Assert.Equal("Admin@fixture.test",port.Email);Assert.Equal(password,port.Password);Assert.Equal("prior",port.Credential);Assert.Equal("invalid_credentials",result.Error!.Code);
    }
    [Theory] [InlineData(null,"123456789012345")] [InlineData("bad","123456789012345")] [InlineData("a@fixture.test",null)]
    public async Task Missing_or_invalid_fields_are_rejected_before_adapter(string? email,string? password)
    {
        var port=new Operations();var result=await new SignInHandler(port).Handle(new(email,password,null),default);Assert.Equal(400,result.Error!.Status);Assert.Equal(0,port.Calls);
    }
    [Fact] public async Task Refresh_and_signout_delegate_only_the_current_context()
    {
        var port=new Operations();Assert.False((await new RefreshHandler(port).Handle(new("current"),default)).Succeeded);Assert.Equal("current",port.Credential);
        var bearer=new TrustedAuthentication(new(Guid.NewGuid(),"Admin",Crm.Domain.Accounts.AccountRole.Admin),Guid.NewGuid(),1,DateTimeOffset.UtcNow.AddDays(1));
        Assert.True((await new SignOutHandler(port).Handle(new(null,bearer),default)).Succeeded);Assert.Same(bearer,port.Bearer);Assert.Null(port.Credential);
    }
    [Fact] public async Task Cancellation_precedes_any_adapter_call()
    {
        var port=new Operations();using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new SignInHandler(port).Handle(new("a@fixture.test","123456789012345",null),cancellation.Token));Assert.Equal(0,port.Calls);
    }
}
