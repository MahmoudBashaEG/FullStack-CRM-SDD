using Crm.Application.Tickets.Contracts;
namespace Crm.Application.Tests.Tickets;

public sealed class EditVersionTests
{
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("AAAAAAAAAAE= ")] [InlineData("AAAA")] [InlineData("\"AAAAAAAAAAE=\"")]
    public void Invalid_or_noncanonical_tokens_are_rejected(string? text) => Assert.False(EditVersion.TryParse(text, out _));
    [Fact]
    public void Canonical_eight_byte_token_is_opaque_and_valid()
    {
        Assert.True(EditVersion.TryParse("AAAAAAAAAAE=", out var bytes)); Assert.Equal(8, bytes.Length); Assert.Equal("AAAAAAAAAAE=", Convert.ToBase64String(bytes));
    }
}
