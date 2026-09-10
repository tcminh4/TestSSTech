namespace PartnerTransactions.Api.Services;

public readonly record struct PartnerVerificationResult(PartnerVerificationStatus Status)
{
    public static PartnerVerificationResult Verified() => new(PartnerVerificationStatus.Verified);
    public static PartnerVerificationResult NotFound() => new(PartnerVerificationStatus.NotFound);
    public static PartnerVerificationResult Unavailable() => new(PartnerVerificationStatus.Unavailable);
}

public enum PartnerVerificationStatus
{
    Verified,
    NotFound,
    Unavailable
}
