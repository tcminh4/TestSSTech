using System.Globalization;

namespace PartnerTransactions.Api.Validation;

public static class CurrencyCodes
{
    public static readonly HashSet<string> Iso4217 = CultureInfo
        .GetCultures(CultureTypes.SpecificCultures)
        .Select(culture =>
        {
            try
            {
                return new RegionInfo(culture.Name).ISOCurrencySymbol;
            }
            catch (ArgumentException)
            {
                return null;
            }
        })
        .Where(code => !string.IsNullOrWhiteSpace(code))
        .Cast<string>()
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static bool IsValid(string? currency) =>
        !string.IsNullOrWhiteSpace(currency) && Iso4217.Contains(currency);
}
