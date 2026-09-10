namespace PartnerTransactions.Api.Services;

public interface IRandomNumberGenerator
{
    double NextDouble();
}

public sealed class SystemRandomNumberGenerator : IRandomNumberGenerator
{
    public double NextDouble() => Random.Shared.NextDouble();
}
