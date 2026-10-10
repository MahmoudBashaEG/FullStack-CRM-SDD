using Crm.Application.Accounts.Commands;
using Crm.Application.Accounts.Contracts;
using Crm.Application.Accounts.Ports;
using Crm.Application.Common;
using Crm.Domain.Accounts;
namespace Crm.Application.Tests.Accounts;

public sealed class RegisterCustomerTests
{
    [Theory] [InlineData(" user@example.test ","user@example.test")] [InlineData("\u0085user@example.test\u0085","user@example.test")] [InlineData("\ufeffuser@example.test\ufeff","\ufeffuser@example.test\ufeff")] [InlineData(" \"multi part\"@example.test ","\"multi part\"@example.test")]
    public async Task Creation_and_signin_share_canonical_identity(string input,string expected)
    {
        var store=new Store();const string password="  exact عربي password  ";var result=await new RegisterCustomerHandler(store).Handle(new("\u0085"+new string('a',200)+"\u0085",input,password),default);
        Assert.True(result.Succeeded);Assert.Equal(expected,store.Account!.Email);Assert.Equal(new string('a',200),store.Account.DisplayName);Assert.Equal(password,store.Account.Password);Assert.Equal(AccountRole.Customer,store.Account.Role);
    }
    private sealed class Store : IAccountRegistrationStore
    {
        public NewAccount? Account;
        public CancellationToken Token;
        public bool Conflict;
        public Task<OperationResult<AccountIdentity>> RegisterAsync(NewAccount account, CancellationToken ct)
        {
            Account=account;Token=ct;
            return Task.FromResult(Conflict ? OperationResult<AccountIdentity>.Fail("registration_failed",400) : OperationResult<AccountIdentity>.Success(new(Guid.NewGuid(),account.DisplayName,account.Role)));
        }
    }
    [Fact] public async Task Customer_only_exact_password_and_common_normalization()
    {
        var store=new Store();using var cancellation=new CancellationTokenSource();var password="  عربي password  ";
        var result=await new RegisterCustomerHandler(store).Handle(new(" Visitor "," visitor@fixture.test ",password),cancellation.Token);
        Assert.True(result.Succeeded);Assert.Equal(AccountRole.Customer,store.Account!.Role);
        Assert.Equal("Visitor",store.Account.DisplayName);Assert.Equal("visitor@fixture.test",store.Account.Email);Assert.Equal(password,store.Account.Password);Assert.Equal(cancellation.Token,store.Token);
        Assert.DoesNotContain(password,System.Text.Json.JsonSerializer.Serialize(result));
    }
    [Theory]
    [InlineData(null,"visitor@fixture.test","123456789012345")]
    [InlineData(" ","visitor@fixture.test","123456789012345")]
    [InlineData("Visitor",null,"123456789012345")]
    [InlineData("Visitor","bad","123456789012345")]
    [InlineData("Visitor","visitor@fixture.test",null)]
    [InlineData("Visitor","visitor@fixture.test","12345678901234")]
    [InlineData("Visitor","visitor@fixture.test","\ud800")]
    public async Task Invalid_input_never_calls_store(string? name,string? email,string? password)
    {
        var store=new Store();var result=await new RegisterCustomerHandler(store).Handle(new(name,email,password),default);
        Assert.Equal("validation_failed",result.Error!.Code);Assert.Equal(400,result.Error.Status);Assert.Null(store.Account);
        Assert.NotNull(result.Error.Errors);
    }
    [Theory] [InlineData(14,false)] [InlineData(15,true)] [InlineData(128,true)] [InlineData(129,false)]
    public async Task Scalar_boundaries_spaces_and_supplementary_Unicode(int count,bool accepted)
    {
        foreach(var password in new[]{new string(' ',count),string.Concat(Enumerable.Repeat("😀",count))})
        {
            var store=new Store();var result=await new RegisterCustomerHandler(store).Handle(new("Visitor","visitor@fixture.test",password),default);
            Assert.Equal(accepted,result.Succeeded);if(accepted)Assert.Equal(password,store.Account!.Password);else Assert.Null(store.Account);
        }
    }
    [Fact] public async Task Sensitive_failure_is_generic_and_has_no_account()
    {
        var result=await new RegisterCustomerHandler(new Store{Conflict=true}).Handle(new("Visitor","visitor@fixture.test","123456789012345"),default);
        Assert.Null(result.Value);Assert.Equal("registration_failed",result.Error!.Code);Assert.Null(result.Error.Errors);
    }
    [Fact] public async Task Cancellation_precedes_store()
    {
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();var store=new Store();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new RegisterCustomerHandler(store).Handle(new("Visitor","visitor@fixture.test","123456789012345"),cancellation.Token));Assert.Null(store.Account);
    }
    [Fact] public async Task Malformed_surrogates_and_overlong_names_never_reach_Identity()
    {
        foreach(var command in new[]{new RegisterCustomerCommand("Visitor","visitor@fixture.test",new string('x',15)+(char)0xd800),new RegisterCustomerCommand(new string('x',201),"visitor@fixture.test","123456789012345")})
        {
            var store=new Store();Assert.Equal("validation_failed",(await new RegisterCustomerHandler(store).Handle(command,default)).Error!.Code);Assert.Null(store.Account);
        }
    }
}
