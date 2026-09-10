using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PartnerTransactions.Api.Messaging;
using PartnerTransactions.Api.Models;
using PartnerTransactions.Api.Services;
using PartnerTransactions.Api.Validation;

namespace PartnerTransactions.Tests;

public sealed class PartnerTransactionServiceTests
{
    private readonly IValidator<PartnerTransactionRequest> _validator = new PartnerTransactionRequestValidator();
    private readonly Mock<IPartnerVerificationClient> _verification = new();
    private readonly Mock<ITransactionMessagePublisher> _publisher = new();
    private readonly PartnerTransactionService _sut;

    public PartnerTransactionServiceTests()
    {
        _sut = new PartnerTransactionService(
            _validator,
            _verification.Object,
            _publisher.Object,
            NullLogger<PartnerTransactionService>.Instance);
    }

    [Fact]
    public async Task Submit_returns_invalid_and_does_not_call_dependencies_when_payload_is_invalid()
    {
        var result = await _sut.SubmitAsync(new PartnerTransactionRequest { Amount = 0 }, CancellationToken.None);

        Assert.Equal(SubmitTransactionOutcome.Invalid, result.Outcome);
        Assert.NotEmpty(result.ValidationErrors);
        _verification.Verify(v => v.VerifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _publisher.Verify(p => p.PublishAsync(It.IsAny<PartnerTransactionMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Submit_returns_not_found_when_partner_is_unknown()
    {
        _verification
            .Setup(v => v.VerifyAsync("P-UNKNOWN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PartnerVerificationResult.NotFound());

        var result = await _sut.SubmitAsync(ValidRequest("P-UNKNOWN"), CancellationToken.None);

        Assert.Equal(SubmitTransactionOutcome.PartnerNotFound, result.Outcome);
        _publisher.Verify(p => p.PublishAsync(It.IsAny<PartnerTransactionMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Submit_returns_unavailable_when_verification_fails_after_retries()
    {
        _verification
            .Setup(v => v.VerifyAsync("P-1001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PartnerVerificationResult.Unavailable());

        var result = await _sut.SubmitAsync(ValidRequest(), CancellationToken.None);

        Assert.Equal(SubmitTransactionOutcome.Unavailable, result.Outcome);
        _publisher.Verify(p => p.PublishAsync(It.IsAny<PartnerTransactionMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Submit_publishes_and_accepts_when_partner_is_verified()
    {
        _verification
            .Setup(v => v.VerifyAsync("P-1001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PartnerVerificationResult.Verified());

        var result = await _sut.SubmitAsync(ValidRequest(), CancellationToken.None);

        Assert.Equal(SubmitTransactionOutcome.Accepted, result.Outcome);
        Assert.Equal("TXN-99823", result.TransactionReference);
        _publisher.Verify(p => p.PublishAsync(
            It.Is<PartnerTransactionMessage>(m =>
                m.PartnerId == "P-1001" &&
                m.TransactionReference == "TXN-99823" &&
                m.Amount == 250.00m &&
                m.Currency == "USD"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Submit_returns_unavailable_when_queue_publish_fails()
    {
        _verification
            .Setup(v => v.VerifyAsync("P-1001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PartnerVerificationResult.Verified());
        _publisher
            .Setup(p => p.PublishAsync(It.IsAny<PartnerTransactionMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MessagePublishException("queue down", new InvalidOperationException("broker")));

        var result = await _sut.SubmitAsync(ValidRequest(), CancellationToken.None);

        Assert.Equal(SubmitTransactionOutcome.Unavailable, result.Outcome);
    }

    private static PartnerTransactionRequest ValidRequest(string partnerId = "P-1001") => new()
    {
        PartnerId = partnerId,
        TransactionReference = "TXN-99823",
        Amount = 250.00m,
        Currency = "USD",
        Timestamp = DateTimeOffset.Parse("2024-05-10T14:30:00Z")
    };
}
