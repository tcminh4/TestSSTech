using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PartnerTransactions.Api.Controllers;
using PartnerTransactions.Api.Options;
using PartnerTransactions.Api.Services;

namespace PartnerTransactions.Tests;

public sealed class PartnerVerificationMockControllerTests
{
    [Fact]
    public void Verify_throws_timeout_when_simulator_says_so()
    {
        var controller = CreateController(nextDouble: 0.1, timeoutProbability: 0.3);

        Assert.Throws<TimeoutException>(() => controller.Verify("P-1001"));
    }

    [Fact]
    public void Verify_returns_ok_for_prefixed_partner()
    {
        var controller = CreateController(nextDouble: 0.9, timeoutProbability: 0.3);

        var result = controller.Verify("P-1001");

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }



    [Fact]
    public void Boundary_value_at_probability_times_out()
    {
        var controller = CreateController(nextDouble: 0.0, timeoutProbability: 0.3);
        Assert.Throws<TimeoutException>(() => controller.Verify("P-1001"));
    }

    private static PartnerVerificationMockController CreateController(double nextDouble, double timeoutProbability)
    {
        var controller = new PartnerVerificationMockController(
            new FixedRandom(nextDouble),
            Options.Create(new PartnerVerificationOptions { TimeoutProbability = timeoutProbability }));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private sealed class FixedRandom : IRandomNumberGenerator
    {
        private readonly double _value;
        public FixedRandom(double value) => _value = value;
        public double NextDouble() => _value;
    }
}
