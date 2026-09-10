using FluentValidation;
using PartnerTransactions.Api.Models;

namespace PartnerTransactions.Api.Validation;

public sealed class PartnerTransactionRequestValidator : AbstractValidator<PartnerTransactionRequest>
{
    public PartnerTransactionRequestValidator()
    {
        RuleFor(x => x.PartnerId)
            .NotEmpty()
            .WithMessage("PartnerId is required.");

        RuleFor(x => x.TransactionReference)
            .NotEmpty()
            .WithMessage("TransactionReference is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than 0.");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Currency is required.")
            .Must(CurrencyCodes.IsValid)
            .WithMessage("Currency must be a valid ISO 4217 code.");

        RuleFor(x => x.Timestamp)
            .NotEmpty()
            .WithMessage("Timestamp is required.");
    }
}
