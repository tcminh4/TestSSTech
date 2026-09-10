namespace PartnerTransactions.Api.Options;

public sealed class PartnerVerificationOptions
{
    public const string SectionName = "PartnerVerification";

    public string BaseUrl { get; set; } = "http://localhost:5236";
    public double TimeoutProbability { get; set; } = 0.30;
}
