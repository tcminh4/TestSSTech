namespace PartnerTransactions.Api.Models;

public sealed class PartnerVerificationResponse
{
    public required string PartnerId { get; init; }
    public required bool IsValid { get; init; }
    public DateTimeOffset VerifiedAt { get; init; } = DateTimeOffset.UtcNow;
}
