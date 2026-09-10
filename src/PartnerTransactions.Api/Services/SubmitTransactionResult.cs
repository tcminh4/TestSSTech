using PartnerTransactions.Api.Models;

namespace PartnerTransactions.Api.Services;

public sealed class SubmitTransactionResult
{
    public SubmitTransactionOutcome Outcome { get; private init; }
    public string? TransactionReference { get; private init; }
    public IReadOnlyList<string> ValidationErrors { get; private init; } = [];

    public static SubmitTransactionResult Accepted(string transactionReference) => new()
    {
        Outcome = SubmitTransactionOutcome.Accepted,
        TransactionReference = transactionReference
    };

    public static SubmitTransactionResult Invalid(IReadOnlyList<string> errors) => new()
    {
        Outcome = SubmitTransactionOutcome.Invalid,
        ValidationErrors = errors
    };

    public static SubmitTransactionResult PartnerNotFound() => new()
    {
        Outcome = SubmitTransactionOutcome.PartnerNotFound
    };

    public static SubmitTransactionResult Unavailable(string message) => new()
    {
        Outcome = SubmitTransactionOutcome.Unavailable,
        ValidationErrors = [message]
    };
}

public enum SubmitTransactionOutcome
{
    Accepted,
    Invalid,
    PartnerNotFound,
    Unavailable
}
