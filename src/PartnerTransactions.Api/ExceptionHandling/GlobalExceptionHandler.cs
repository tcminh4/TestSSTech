using Microsoft.AspNetCore.Diagnostics;
using PartnerTransactions.Api.Models;

namespace PartnerTransactions.Api.ExceptionHandling;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, error, message) = Map(exception);

        if (statusCode >= 500)
        {
            _logger.LogError(exception, "Unhandled exception");
        }
        else
        {
            _logger.LogWarning(exception, "Handled exception {Error}", error);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            new ApiErrorResponse
            {
                Error = error,
                Message = message,
                TraceId = httpContext.TraceIdentifier
            },
            cancellationToken);

        return true;
    }

    public static (int StatusCode, string Error, string Message) Map(Exception exception) =>
        exception switch
        {
            TimeoutException => (StatusCodes.Status504GatewayTimeout, "timeout", "The upstream service timed out."),
            TaskCanceledException => (StatusCodes.Status504GatewayTimeout, "timeout", "The request was canceled or timed out."),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "invalid_request", "The request payload is invalid."),
            _ => (StatusCodes.Status500InternalServerError, "internal_error", "An unexpected error occurred.")
        };
}
