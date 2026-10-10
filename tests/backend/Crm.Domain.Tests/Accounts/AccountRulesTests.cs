using Crm.Domain.Accounts;
namespace Crm.Domain.Tests.Accounts;

public sealed class AccountRulesTests
{
    [Theory] [InlineData(" \t")] [InlineData("\u0085")] [InlineData("\u00a0\u202f")]
    public void Display_name_vectors_count_normalized_scalars(string whitespace)
    {
        Assert.True(AccountRules.DisplayNameAccepted(whitespace+string.Concat(Enumerable.Repeat("😀",200))+whitespace));
        Assert.False(AccountRules.DisplayNameAccepted(whitespace+string.Concat(Enumerable.Repeat("😀",201))+whitespace));Assert.False(AccountRules.DisplayNameAccepted(whitespace));
        Assert.True(AccountRules.DisplayNameAccepted("\ufeff"));Assert.False(AccountRules.DisplayNameAccepted("\ud800"));
    }
    [Fact] public void Exactly_three_fixed_roles() => Assert.Equal(["Customer", "Agent", "Admin"], Enum.GetNames<AccountRole>());
    [Theory]
    [InlineData(14, false)] [InlineData(15, true)] [InlineData(128, true)] [InlineData(129, false)]
    public void Password_boundary(int length, bool expected) => Assert.Equal(expected, AccountRules.PasswordAccepted(new string('a', length)));
    [Fact] public void Spaces_and_unicode_are_password_characters()
    {
        Assert.True(AccountRules.PasswordAccepted(new string(' ', 15)));
        Assert.True(AccountRules.PasswordAccepted(string.Concat(Enumerable.Repeat("😀", 128))));
        Assert.False(AccountRules.PasswordAccepted(string.Concat(Enumerable.Repeat("😀", 129))));
        Assert.True(AccountRules.TryScalarLength("e\u0301😀", out var count)); Assert.Equal(3, count);
    }
    [Theory] [InlineData(null)] [InlineData("")] [InlineData("\uD800")] [InlineData("\uDC00")]
    public void Invalid_scalar_sequences_rejected(string? value) => Assert.False(AccountRules.PasswordAccepted(value));
    [Fact] public void Display_name_trimmed_scalar_limit()
    {
        Assert.False(AccountRules.DisplayNameAccepted("  "));
        Assert.True(AccountRules.DisplayNameAccepted("  مريم  "));
        Assert.True(AccountRules.DisplayNameAccepted(string.Concat(Enumerable.Repeat("😀", 200))));
        Assert.False(AccountRules.DisplayNameAccepted(new string('x', 201)));
    }
}
