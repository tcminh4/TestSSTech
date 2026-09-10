namespace PartnerTransactions.Api.Messaging;

public sealed class MessagePublishException : Exception
{
    public MessagePublishException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
