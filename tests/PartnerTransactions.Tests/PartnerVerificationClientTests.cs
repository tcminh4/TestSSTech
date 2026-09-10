using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PartnerTransactions.Api.Models;
using PartnerTransactions.Api.Services;

namespace PartnerTransactions.Tests;

public sealed class PartnerVerificationClientTests
{
    [Fact]
    public async Task Verify_returns_verified_for_successful_payload()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new PartnerVerificationResponse
            {
                PartnerId = "P-1001",
                IsValid = true
            })
        });

        var client = CreateClient(handler);
        var result = await client.VerifyAsync("P-1001", CancellationToken.None);

        Assert.Equal(PartnerVerificationStatus.Verified, result.Status);
    }

    [Fact]
    public async Task Verify_returns_not_found_for_404()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateClient(handler);

        var result = await client.VerifyAsync("X-1", CancellationToken.None);

        Assert.Equal(PartnerVerificationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Verify_returns_not_found_when_payload_is_invalid_partner()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new PartnerVerificationResponse
            {
                PartnerId = "P-1001",
                IsValid = false
            })
        });

        var client = CreateClient(handler);
        var result = await client.VerifyAsync("P-1001", CancellationToken.None);

        Assert.Equal(PartnerVerificationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Verify_returns_unavailable_for_gateway_timeout()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.GatewayTimeout));
        var client = CreateClient(handler);

        var result = await client.VerifyAsync("P-1001", CancellationToken.None);

        Assert.Equal(PartnerVerificationStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task Verify_returns_unavailable_when_http_throws()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("connection reset"));
        var client = CreateClient(handler);

        var result = await client.VerifyAsync("P-1001", CancellationToken.None);

        Assert.Equal(PartnerVerificationStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task Verify_does_not_swallow_caller_cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = new StubHandler(_ => throw new TaskCanceledException());
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<TaskCanceledException>(() => client.VerifyAsync("P-1001", cts.Token));
    }

    private static PartnerVerificationClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }, NullLogger<PartnerVerificationClient>.Instance);

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }
}
