using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Timeout;

namespace PartnerTransactions.Api.Resilience;

public static class PartnerVerificationResilience
{
    public const int MaxRetryAttempts = 3;

    public static void Configure(HttpStandardResilienceOptions options)
    {
        options.Retry.MaxRetryAttempts = MaxRetryAttempts;
        options.Retry.BackoffType = DelayBackoffType.Constant;
        options.Retry.Delay = TimeSpan.FromMilliseconds(50);
        options.Retry.ShouldHandle = args =>
        {
            if (args.Outcome.Exception is HttpRequestException
                or TimeoutRejectedException
                or TimeoutException
                or TaskCanceledException)
            {
                return PredicateResult.True();
            }

            var statusCode = args.Outcome.Result?.StatusCode;
            return (statusCode is not null && (int)statusCode >= 500)
                ? PredicateResult.True()
                : PredicateResult.False();
        };

        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);

        // Keep the breaker from opening during short bursts of mock timeouts.
        options.CircuitBreaker.FailureRatio = 1.0;
        options.CircuitBreaker.MinimumThroughput = 100;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(1);
    }
}
