using PartnerTransactions.Api.Services;

namespace PartnerTransactions.Tests;

public sealed class SystemRandomNumberGeneratorTests
{
    [Fact]
    public void NextDouble_returns_value_in_unit_interval()
    {
        var generator = new SystemRandomNumberGenerator();
        var value = generator.NextDouble();
        Assert.InRange(value, 0.0, 1.0);
    }
}
