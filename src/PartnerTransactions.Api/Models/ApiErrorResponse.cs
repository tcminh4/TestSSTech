namespace PartnerTransactions.Api.Models;

public sealed class ApiErrorResponse
{
    public required string Error { get; init; }
    public required string Message { get; init; }
    public string? TraceId { get; init; }
    public IReadOnlyList<string>? Details { get; init; }
}
