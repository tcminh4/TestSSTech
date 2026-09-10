using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using PartnerTransactions.Api.Messaging;
using PartnerTransactions.Api.Models;
using PartnerTransactions.Api.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace PartnerTransactions.Tests;

public sealed class PartnerTransactionsApiFactory : WebApplicationFactory<Program>
{
    public InMemoryTransactionPublisher Publisher { get; } = new();
    public Queue<double> RandomValues { get; } = new();
    public PartnerVerificationStatus VerificationStatus { get; set; } = PartnerVerificationStatus.Verified;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            Remove<ITransactionMessagePublisher>(services);
            Remove<IRandomNumberGenerator>(services);
            Remove<IPartnerVerificationClient>(services);

            services.AddSingleton<ITransactionMessagePublisher>(Publisher);
            services.AddSingleton<IRandomNumberGenerator>(new QueueRandom(RandomValues));
            services.AddSingleton<IPartnerVerificationClient>(new StubVerificationClient(this));
        });
    }

    private static void Remove<T>(IServiceCollection services)
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(T)).ToList();
        foreach (var descriptor in descriptors)
        {
            services.Remove(descriptor);
        }
    }

    public sealed class InMemoryTransactionPublisher : ITransactionMessagePublisher
    {
        public List<PartnerTransactionMessage> Messages { get; } = [];

        public Task PublishAsync(PartnerTransactionMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class QueueRandom : IRandomNumberGenerator
    {
        private readonly Queue<double> _values;
        public QueueRandom(Queue<double> values) => _values = values;
        public double NextDouble() => _values.Count > 0 ? _values.Dequeue() : 0.9;
    }

    private sealed class StubVerificationClient : IPartnerVerificationClient
    {
        private readonly PartnerTransactionsApiFactory _factory;
        public StubVerificationClient(PartnerTransactionsApiFactory factory) => _factory = factory;

        public Task<PartnerVerificationResult> VerifyAsync(string partnerId, CancellationToken cancellationToken) =>
            Task.FromResult(new PartnerVerificationResult(_factory.VerificationStatus));
    }
}

public sealed class PartnerTransactionsApiTests : IClassFixture<PartnerTransactionsApiFactory>
{
    private readonly PartnerTransactionsApiFactory _factory;

    public PartnerTransactionsApiTests(PartnerTransactionsApiFactory factory)
    {
        _factory = factory;
        _factory.Publisher.Messages.Clear();
        _factory.RandomValues.Clear();
        _factory.VerificationStatus = PartnerVerificationStatus.Verified;
    }

    [Fact]
    public async Task Submit_without_credentials_returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/partner/transactions", ValidPayload());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Submit_with_api_key_accepts_valid_transaction()
    {
        var client = CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/v1/partner/transactions", ValidPayload());

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Single(_factory.Publisher.Messages);
        Assert.Equal("TXN-99823", _factory.Publisher.Messages[0].TransactionReference);
    }

    [Fact]
    public async Task Submit_with_jwt_accepts_valid_transaction()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt());

        var response = await client.PostAsJsonAsync("/api/v1/partner/transactions", ValidPayload());

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Submit_invalid_payload_returns_400()
    {
        var client = CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/v1/partner/transactions", new PartnerTransactionRequest
        {
            PartnerId = "P-1001",
            Amount = 0
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("validation_error", body!.Error);
        Assert.NotNull(body.Details);
    }

    [Fact]
    public async Task Submit_unknown_partner_returns_404()
    {
        _factory.VerificationStatus = PartnerVerificationStatus.NotFound;
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/v1/partner/transactions", ValidPayload());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(_factory.Publisher.Messages);
    }

    [Fact]
    public async Task Submit_returns_503_when_verification_unavailable()
    {
        _factory.VerificationStatus = PartnerVerificationStatus.Unavailable;
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/v1/partner/transactions", ValidPayload());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("service_unavailable", body!.Error);
    }

    [Fact]
    public async Task Mock_verification_timeout_is_formatted_by_global_handler()
    {
        _factory.RandomValues.Enqueue(0.01);
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/internal/partner-verification/P-1001");

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("timeout", body!.Error);
        Assert.False(string.IsNullOrWhiteSpace(body.TraceId));
    }

    [Fact]
    public async Task Health_returns_ok()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "local-dev-api-key");
        return client;
    }

    private static object ValidPayload() => new
    {
        partnerId = "P-1001",
        transactionReference = "TXN-99823",
        amount = 250.00,
        currency = "USD",
        timestamp = "2024-05-10T14:30:00Z"
    };

    private static string CreateJwt()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("change-me-to-a-long-random-signing-key!"));
        var token = new JwtSecurityToken(
            issuer: "PartnerTransactions",
            audience: "PartnerTransactions",
            claims: new[] { new Claim(ClaimTypes.Name, "jwt-client") },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
