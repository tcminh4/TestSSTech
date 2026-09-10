using System.Net;
using Microsoft.Extensions.DependencyInjection;
using PartnerTransactions.Api.Resilience;
using PartnerTransactions.Api.Services;

namespace PartnerTransactions.Tests;

public sealed class PartnerVerificationResilienceTests
{
    [Fact]
    public async Task Http_client_retries_transient_failures_then_succeeds()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.GatewayTimeout),
            new HttpResponseMessage(HttpStatusCode.GatewayTimeout),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"partnerId":"P-1001","isValid":true}""", System.Text.Encoding.UTF8, "application/json")
            });

        var client = CreateClient(handler);
        var result = await client.VerifyAsync("P-1001", CancellationToken.None);

        Assert.Equal(PartnerVerificationStatus.Verified, result.Status);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task Http_client_stops_after_configured_retries_and_does_not_throw()
    {
        var responses = Enumerable
            .Range(0, PartnerVerificationResilience.MaxRetryAttempts + 1)
            .Select(_ => new HttpResponseMessage(HttpStatusCode.GatewayTimeout))
            .ToArray();
        var handler = new SequenceHandler(responses);

        var client = CreateClient(handler);
        var result = await client.VerifyAsync("P-1001", CancellationToken.None);

        Assert.Equal(PartnerVerificationStatus.Unavailable, result.Status);
        Assert.Equal(PartnerVerificationResilience.MaxRetryAttempts + 1, handler.CallCount);
    }

    [Fact]
    public async Task Http_client_does_not_retry_not_found()
    {
        var handler = new SequenceHandler(new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateClient(handler);

        var result = await client.VerifyAsync("bad-id", CancellationToken.None);

        Assert.Equal(PartnerVerificationStatus.NotFound, result.Status);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Http_client_retries_timeout_exceptions()
    {
        var handler = new SequenceHandler(
            () => throw new TimeoutException("simulated"),
            () => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"partnerId":"P-1001","isValid":true}""", System.Text.Encoding.UTF8, "application/json")
            });

        var client = CreateClient(handler);
        var result = await client.VerifyAsync("P-1001", CancellationToken.None);

        Assert.Equal(PartnerVerificationStatus.Verified, result.Status);
        Assert.Equal(2, handler.CallCount);
    }

    private static IPartnerVerificationClient CreateClient(HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient<IPartnerVerificationClient, PartnerVerificationClient>(client =>
            {
                client.BaseAddress = new Uri("http://partner-verification");
            })
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddStandardResilienceHandler(PartnerVerificationResilience.Configure);

        return services.BuildServiceProvider().GetRequiredService<IPartnerVerificationClient>();
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage>[] _actions;
        private int _index;

        public SequenceHandler(params HttpResponseMessage[] responses)
            : this(responses.Select(r => (Func<HttpResponseMessage>)(() => r)).ToArray())
        {
        }

        public SequenceHandler(params Func<HttpResponseMessage>[] actions)
        {
            _actions = actions;
        }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var action = _actions[Math.Min(_index, _actions.Length - 1)];
            _index++;
            return Task.FromResult(action());
        }
    }
}
