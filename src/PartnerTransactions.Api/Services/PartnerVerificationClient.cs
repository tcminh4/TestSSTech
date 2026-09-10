using System.Net;
using System.Net.Http.Json;
using PartnerTransactions.Api.Models;

namespace PartnerTransactions.Api.Services;

public sealed class PartnerVerificationClient : IPartnerVerificationClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PartnerVerificationClient> _logger;

    public PartnerVerificationClient(HttpClient httpClient, ILogger<PartnerVerificationClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PartnerVerificationResult> VerifyAsync(string partnerId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                $"internal/partner-verification/{Uri.EscapeDataString(partnerId)}",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return PartnerVerificationResult.NotFound();
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Partner verification returned {StatusCode} for {PartnerId}",
                    (int)response.StatusCode,
                    partnerId);
                return PartnerVerificationResult.Unavailable();
            }

            var payload = await response.Content.ReadFromJsonAsync<PartnerVerificationResponse>(cancellationToken);
            return payload?.IsValid == true
                ? PartnerVerificationResult.Verified()
                : PartnerVerificationResult.NotFound();
        }
        catch (Exception ex) when (IsTransient(ex, cancellationToken))
        {
            _logger.LogWarning(ex, "Partner verification failed for {PartnerId} after retries", partnerId);
            return PartnerVerificationResult.Unavailable();
        }
    }

    private static bool IsTransient(Exception exception, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return exception is HttpRequestException
            or TaskCanceledException
            or TimeoutException
            or OperationCanceledException;
    }
}
