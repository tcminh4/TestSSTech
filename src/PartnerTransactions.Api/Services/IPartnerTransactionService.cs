using PartnerTransactions.Api.Models;

namespace PartnerTransactions.Api.Services;

public interface IPartnerTransactionService
{
    Task<SubmitTransactionResult> SubmitAsync(PartnerTransactionRequest request, CancellationToken cancellationToken);
}
