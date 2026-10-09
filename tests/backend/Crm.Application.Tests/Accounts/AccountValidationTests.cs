using Crm.Application.Accounts.Validation;
namespace Crm.Application.Tests.Accounts;

public sealed class AccountValidationTests
{
    [Fact] public void Email_trim_normalize_and_case_equivalence()
    {
        Assert.True(AccountValidation.TryNormalizeEmail("  Staff@Example.test ", out var email, out var normalized));
        Assert.Equal("Staff@Example.test", email); Assert.Equal("STAFF@EXAMPLE.TEST", normalized);
        Assert.True(AccountValidation.TryNormalizeEmail("staff@example.test", out _, out var other)); Assert.Equal(normalized, other);
    }
    [Theory] [InlineData(null)] [InlineData("")] [InlineData("x")] [InlineData("Name <user@example.test>")] [InlineData("x\n@example.test")]
    public void Invalid_addresses(string? value) => Assert.False(AccountValidation.TryNormalizeEmail(value, out _, out _));
    [Fact] public void Email_bound_is_utf16()
    {
        Assert.False(AccountValidation.TryNormalizeEmail(new string('a', 250)+"@example.test", out _, out _));
    }
}
