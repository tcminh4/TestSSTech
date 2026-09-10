using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PartnerTransactions.Api.Controllers;
using PartnerTransactions.Api.Models;
using PartnerTransactions.Api.Services;

namespace PartnerTransactions.Tests;

public sealed class PartnerTransactionsControllerTests
{
    private readonly Mock<IPartnerTransactionService> _service = new();
    private readonly PartnerTransactionsController _controller;

    public PartnerTransactionsControllerTests()
    {
        _controller = new PartnerTransactionsController(_service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public async Task Create_returns_202_when_accepted()
    {
        _service
            .Setup(s => s.SubmitAsync(It.IsAny<PartnerTransactionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SubmitTransactionResult.Accepted("TXN-99823"));

        var result = await _controller.Create(ValidRequest(), CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result);
        Assert.Equal(StatusCodes.Status202Accepted, accepted.StatusCode);
    }

    [Fact]
    public async Task Create_returns_400_when_invalid()
    {
        _service
            .Setup(s => s.SubmitAsync(It.IsAny<PartnerTransactionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SubmitTransactionResult.Invalid(["Amount must be greater than 0."]));

        var result = await _controller.Create(ValidRequest(), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var body = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal("validation_error", body.Error);
    }

    [Fact]
    public async Task Create_returns_404_when_partner_missing()
    {
        _service
            .Setup(s => s.SubmitAsync(It.IsAny<PartnerTransactionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SubmitTransactionResult.PartnerNotFound());

        var result = await _controller.Create(ValidRequest(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Create_returns_503_when_unavailable()
    {
        _service
            .Setup(s => s.SubmitAsync(It.IsAny<PartnerTransactionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SubmitTransactionResult.Unavailable("down"));

        var result = await _controller.Create(ValidRequest(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
    }

    private static PartnerTransactionRequest ValidRequest() => new()
    {
        PartnerId = "P-1001",
        TransactionReference = "TXN-99823",
        Amount = 250.00m,
        Currency = "USD",
        Timestamp = DateTimeOffset.Parse("2024-05-10T14:30:00Z")
    };
}
