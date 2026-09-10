namespace PartnerTransactions.Api.Options;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string ApiKey { get; set; } = string.Empty;
    public JwtOptions Jwt { get; set; } = new();
}

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "PartnerTransactions";
    public string Audience { get; set; } = "PartnerTransactions";
    public string SigningKey { get; set; } = string.Empty;
}
