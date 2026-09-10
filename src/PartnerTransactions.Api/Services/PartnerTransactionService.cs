using FluentValidation;
using PartnerTransactions.Api.Messaging;
using PartnerTransactions.Api.Models;

namespace PartnerTransactions.Api.Services;

public sealed class PartnerTransactionService : IPartnerTransactionService
{
    private readonly IValidator<PartnerTransactionRequest> _validator;
    private readonly IPartnerVerificationClient _verificationClient;
    private readonly ITransactionMessagePublisher _publisher;
    private readonly ILogger<PartnerTransactionService> _logger;

    public PartnerTransactionService(
        IValidator<PartnerTransactionRequest> validator,
        IPartnerVerificationClient verificationClient,
        ITransactionMessagePublisher publisher,
        ILogger<PartnerTransactionService> logger)
    {
        _validator = validator;
        _verificationClient = verificationClient;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<SubmitTransactionResult> SubmitAsync(
        PartnerTransactionRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return SubmitTransactionResult.Invalid(
                validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var verification = await _verificationClient.VerifyAsync(request.PartnerId!, cancellationToken);
        if (verification.Status == PartnerVerificationStatus.NotFound)
        {
            return SubmitTransactionResult.PartnerNotFound();
        }

        if (verification.Status == PartnerVerificationStatus.Unavailable)
        {
            return SubmitTransactionResult.Unavailable(
                "Partner verification is temporarily unavailable. Please retry later.");
        }

        var message = new PartnerTransactionMessage
        {
            PartnerId = request.PartnerId!,
            TransactionReference = request.TransactionReference!,
            Amount = request.Amount,
            Currency = request.Currency!,
            Timestamp = request.Timestamp!.Value
        };

        try
        {
            await _publisher.PublishAsync(message, cancellationToken);
        }
        catch (MessagePublishException ex)
        {
            _logger.LogError(ex, "Failed to enqueue transaction {TransactionReference}", request.TransactionReference);
            return SubmitTransactionResult.Unavailable("The transaction could not be queued. Please retry later.");
        }

        return SubmitTransactionResult.Accepted(request.TransactionReference!);
    }
}
