using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PartnerTransactions.Api.ExceptionHandling;
using PartnerTransactions.Api.Models;

namespace PartnerTransactions.Tests;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task Timeout_exception_is_formatted_as_504()
    {
        var (context, handled) = await HandleAsync(new TimeoutException("boom"));

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status504GatewayTimeout, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await System.Text.Json.JsonSerializer.DeserializeAsync<ApiErrorResponse>(
            context.Response.Body,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal("timeout", body!.Error);
        Assert.Equal(context.TraceIdentifier, body.TraceId);
    }

    [Fact]
    public async Task Unexpected_exception_is_formatted_as_500()
    {
        var (context, handled) = await HandleAsync(new InvalidOperationException("nope"));

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await System.Text.Json.JsonSerializer.DeserializeAsync<ApiErrorResponse>(
            context.Response.Body,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal("internal_error", body!.Error);
    }

    [Fact]
    public void Map_covers_known_exception_types()
    {
        Assert.Equal(StatusCodes.Status504GatewayTimeout, GlobalExceptionHandler.Map(new TaskCanceledException()).StatusCode);
        Assert.Equal("invalid_request", GlobalExceptionHandler.Map(new BadHttpRequestException("bad")).Error);
    }

    private static async Task<(DefaultHttpContext Context, bool Handled)> HandleAsync(Exception exception)
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-123",
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };
        context.Response.Body = new MemoryStream();

        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);
        return (context, handled);
    }
}
