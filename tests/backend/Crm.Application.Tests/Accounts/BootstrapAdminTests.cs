using Crm.Application.Accounts.Commands;
using Crm.Application.Accounts.Contracts;
using Crm.Application.Accounts.Ports;
using Crm.Application.Common;
using Crm.Domain.Accounts;
namespace Crm.Application.Tests.Accounts;

public sealed class BootstrapAdminTests
{
    private sealed class Store(bool complete = false, bool conflict = false) : IInitialAdminBootstrapStore
    {
        public NewAccount? Account;
        public async Task<OperationResult<AdminBootstrapResult>> BootstrapAsync(Func<CancellationToken, Task<OperationResult<NewAccount>>> input, CancellationToken ct)
        {
            if (complete) return OperationResult<AdminBootstrapResult>.Success(new("already_complete", Guid.Empty));
            var result = await input(ct);
            if (!result.Succeeded) return new(null, result.Error);
            if (conflict) return OperationResult<AdminBootstrapResult>.Fail("bootstrap_invalid", 400);
            Account = result.Value;
            return OperationResult<AdminBootstrapResult>.Success(new("created", Guid.NewGuid()));
        }
    }
    [Theory]
    [InlineData(null, "admin@fixture.test", "123456789012345")]
    [InlineData("Admin", null, "123456789012345")]
    [InlineData("Admin", "bad", "123456789012345")]
    [InlineData("Admin", "admin@fixture.test", null)]
    [InlineData("Admin", "admin@fixture.test", "12345678901234")]
    public async Task Invalid_input_creates_nothing(string? name, string? email, string? password)
    {
        var store = new Store();
        var result = await new BootstrapInitialAdminHandler(store).Handle(new(_ => Task.FromResult<AdminBootstrapCredentials?>(new(name,email,password))), default);
        Assert.False(result.Succeeded); Assert.Null(store.Account); Assert.Equal("bootstrap_invalid", result.Error!.Code);
    }
    [Fact] public async Task Completed_setup_does_not_request_credentials()
    {
        var result = await new BootstrapInitialAdminHandler(new Store(true)).Handle(new(_ => throw new Exception("Input must not run")), default);
        Assert.Equal("already_complete", result.Value!.Status);
    }
    [Fact] public async Task Creates_only_Admin_and_preserves_exact_password()
    {
        var store = new Store(); var password = "  عربي password  ";
        var result = await new BootstrapInitialAdminHandler(store).Handle(new(_ => Task.FromResult<AdminBootstrapCredentials?>(new(" Admin "," admin@fixture.test ",password))), default);
        Assert.True(result.Succeeded); Assert.Equal(AccountRole.Admin,store.Account!.Role);
        Assert.Equal(password,store.Account.Password); Assert.Equal("Admin",store.Account.DisplayName); Assert.Equal("admin@fixture.test",store.Account.Email);
        Assert.DoesNotContain(password,System.Text.Json.JsonSerializer.Serialize(result));
    }
    [Fact] public async Task Missing_and_conflicting_input_fail_without_an_account()
    {
        var store = new Store(); var handler = new BootstrapInitialAdminHandler(store);
        Assert.False((await handler.Handle(new(_ => Task.FromResult<AdminBootstrapCredentials?>(null)),default)).Succeeded);
        var conflict = new Store(conflict:true);
        Assert.False((await new BootstrapInitialAdminHandler(conflict).Handle(new(_ => Task.FromResult<AdminBootstrapCredentials?>(new("Admin","used@fixture.test","123456789012345"))),default)).Succeeded);
        Assert.Null(conflict.Account);
    }
    [Fact] public async Task Cancellation_is_propagated_before_input()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new BootstrapInitialAdminHandler(new Store()).Handle(new(_ => throw new Exception()),cancellation.Token));
    }
}
