using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PartnerTransactions.Api.Models;
using PartnerTransactions.Api.Security;
using PartnerTransactions.Api.Services;

namespace PartnerTransactions.Api.Controllers;

[ApiController]
[Route("api/v1/partner/transactions")]
[Authorize(AuthenticationSchemes = $"{ApiKeyAuthenticationHandler.SchemeName},Bearer")]
public sealed class PartnerTransactionsController : ControllerBase
{
    private readonly IPartnerTransactionService _service;

    public PartnerTransactionsController(IPartnerTransactionService service)
    {
        _service = service;
    }

    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create(
        [FromBody] PartnerTransactionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.SubmitAsync(request, cancellationToken);

        return result.Outcome switch
        {
            SubmitTransactionOutcome.Accepted => Accepted(new
            {
                status = "accepted",
                transactionReference = result.TransactionReference
            }),
            SubmitTransactionOutcome.Invalid => BadRequest(new ApiErrorResponse
            {
                Error = "validation_error",
                Message = "The transaction payload is invalid.",
                TraceId = HttpContext.TraceIdentifier,
                Details = result.ValidationErrors
            }),
            SubmitTransactionOutcome.PartnerNotFound => NotFound(new ApiErrorResponse
            {
                Error = "partner_not_found",
                Message = "The partner could not be verified.",
                TraceId = HttpContext.TraceIdentifier
            }),
            SubmitTransactionOutcome.Unavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new ApiErrorResponse
            {
                Error = "service_unavailable",
                Message = result.ValidationErrors.FirstOrDefault() ?? "The service is temporarily unavailable.",
                TraceId = HttpContext.TraceIdentifier
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
            {
                Error = "internal_error",
                Message = "An unexpected error occurred.",
                TraceId = HttpContext.TraceIdentifier
            })
        };
    }
}
