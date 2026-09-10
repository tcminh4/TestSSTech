using PartnerTransactions.Api.Models;
using PartnerTransactions.Api.Validation;

namespace PartnerTransactions.Tests;

public sealed class PartnerTransactionRequestValidatorTests
{
    private readonly PartnerTransactionRequestValidator _validator = new();

    [Fact]
    public void Valid_request_passes()
    {
        var result = _validator.Validate(CreateValidRequest());
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void PartnerId_is_required(string? partnerId)
    {
        var request = CreateValidRequest() with { PartnerId = partnerId };
        var result = _validator.Validate(request);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PartnerTransactionRequest.PartnerId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TransactionReference_is_required(string? transactionReference)
    {
        var request = CreateValidRequest() with { TransactionReference = transactionReference };
        var result = _validator.Validate(request);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PartnerTransactionRequest.TransactionReference));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-250.00)]
    public void Amount_must_be_greater_than_zero(decimal amount)
    {
        var request = CreateValidRequest() with { Amount = amount };
        var result = _validator.Validate(request);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PartnerTransactionRequest.Amount));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("XXXX")]
    public void Currency_must_be_valid_iso_4217(string? currency)
    {
        var request = CreateValidRequest() with { Currency = currency };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PartnerTransactionRequest.Currency));
    }

    [Fact]
    public void Timestamp_is_required()
    {
        var request = CreateValidRequest() with { Timestamp = null };
        var result = _validator.Validate(request);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PartnerTransactionRequest.Timestamp));
    }

    [Fact]
    public void Multiple_missing_fields_are_reported_together()
    {
        var result = _validator.Validate(new PartnerTransactionRequest());
        Assert.True(result.Errors.Count >= 4);
    }

    private static PartnerTransactionRequest CreateValidRequest() => new()
    {
        PartnerId = "P-1001",
        TransactionReference = "TXN-99823",
        Amount = 250.00m,
        Currency = "USD",
        Timestamp = DateTimeOffset.Parse("2024-05-10T14:30:00Z")
    };
}
