using PartnerTransactions.Api.Validation;

namespace PartnerTransactions.Tests;

public sealed class CurrencyCodesTests
{
    [Theory]
    [InlineData("USD")]
    [InlineData("usd")]
    [InlineData("EUR")]
    [InlineData("THB")]
    public void Recognizes_common_iso_codes(string code)
    {
        Assert.True(CurrencyCodes.IsValid(code));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ZZZ")]
    public void Rejects_invalid_codes(string? code)
    {
        Assert.False(CurrencyCodes.IsValid(code));
    }
}
