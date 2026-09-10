using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PartnerTransactions.Api.Models;
using PartnerTransactions.Api.Options;
using PartnerTransactions.Api.Services;

namespace PartnerTransactions.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("internal/partner-verification")]
public sealed class PartnerVerificationMockController : ControllerBase
{
    private readonly IRandomNumberGenerator _random;
    private readonly PartnerVerificationOptions _options;

    public PartnerVerificationMockController(
        IRandomNumberGenerator random,
        IOptions<PartnerVerificationOptions> options)
    {
        _random = random;
        _options = options.Value;
    }

    [HttpGet("{partnerId}")]
    [ProducesResponseType(typeof(PartnerVerificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Verify(string partnerId)
    {
        if (_random.NextDouble() < _options.TimeoutProbability)
        {
            throw new TimeoutException("Partner Verification API timed out.");
        }

        return Ok(new PartnerVerificationResponse
        {
            PartnerId = partnerId,
            IsValid = true,
            VerifiedAt = DateTimeOffset.UtcNow
        });
    }
}
