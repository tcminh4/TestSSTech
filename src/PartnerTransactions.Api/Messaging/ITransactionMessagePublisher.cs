using PartnerTransactions.Api.Models;

namespace PartnerTransactions.Api.Messaging;

public interface ITransactionMessagePublisher
{
    Task PublishAsync(PartnerTransactionMessage message, CancellationToken cancellationToken);
}
