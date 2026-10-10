using Crm.Application.Accounts.Validation;
namespace Crm.Application.Tests.Accounts;

public sealed class AccountValidationTests
{
    [Theory] [InlineData(" user@example.test ","user@example.test",true)] [InlineData("\u0085user@example.test\u0085","user@example.test",true)] [InlineData("\ufeffuser@example.test\ufeff","\ufeffuser@example.test\ufeff",true)] [InlineData(" \"multi part\"@example.test ","\"multi part\"@example.test",true)] [InlineData("bad","",false)]
    public void Canonical_email_vectors_preserve_identity_and_case_equivalence(string input,string expected,bool valid)
    {
        Assert.Equal(valid,AccountValidation.TryNormalizeEmail(input,out var value,out var normalized));if(!valid)return;
        Assert.Equal(expected,value);Assert.True(AccountValidation.TryNormalizeEmail(input.ToUpperInvariant(),out _,out var equivalent));Assert.Equal(normalized,equivalent);
        if(expected.Contains('\ufeff')){Assert.True(AccountValidation.TryNormalizeEmail("user@example.test",out _,out var ordinary));Assert.NotEqual(ordinary,normalized);}
    }
    [Theory] [InlineData("\u0085")] [InlineData(" \t\r\n")] [InlineData("\u00a0")] [InlineData("\u1680")] [InlineData("\u2000\u200a")] [InlineData("\u2028\u2029")] [InlineData("\u202f\u205f\u3000")]
    public void Email_surrounding_whitespace_is_trimmed_without_touching_internal_quoted_spaces(string whitespace)
    {
        const string address="\"multi part\"@example.test";
        Assert.True(AccountValidation.TryNormalizeEmail(whitespace+address+whitespace,out var email,out var normalized));Assert.Equal(address,email);Assert.Equal(address.ToUpperInvariant(),normalized);
        Assert.False(AccountValidation.TryNormalizeEmail("user\u0085@example.test",out _,out _));
    }
    [Theory] [InlineData("\"multi part\"@example.test")] [InlineData("\"part@inside\"@example.test")] [InlineData("\"part<>inside\"@example.test")] [InlineData("مريم@example.test")]
    public void Supported_single_addresses_preserve_internal_characters(string input)
    {
        Assert.True(AccountValidation.TryNormalizeEmail("  "+input+"  ",out var email,out var normalized));
        Assert.Equal(input,email);Assert.Equal(input.Normalize().ToUpperInvariant(),normalized);
        Assert.True(AccountValidation.TryNormalizeEmail(input.ToUpperInvariant(),out _,out var equivalent));Assert.Equal(normalized,equivalent);
    }
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
