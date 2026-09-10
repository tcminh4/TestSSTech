namespace PartnerTransactions.Api.Services;

public interface IPartnerVerificationClient
{
    Task<PartnerVerificationResult> VerifyAsync(string partnerId, CancellationToken cancellationToken);
}
