namespace PartnerTransactions.Api.Models;

public sealed class PartnerTransactionMessage
{
    public required string PartnerId { get; init; }
    public required string TransactionReference { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public DateTimeOffset PublishedAt { get; init; } = DateTimeOffset.UtcNow;
}
