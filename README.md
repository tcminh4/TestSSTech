# Partner Transactions API

.NET 8 Web API that accepts a partner transaction, verifies the partner against a mock upstream API, then publishes the payload to RabbitMQ.

## Architecture

The HTTP controller stays thin. `PartnerTransactionService` owns the flow: **validate → verify partner → publish**.

- **Validation** — FluentValidation. Amount must be `> 0`, currency must be ISO 4217, all fields required. Failures return a consistent `400` body.
- **Partner verification** — `GET /internal/partner-verification/{partnerId}` stands in for an external service. It throws `TimeoutException` 30% of the time and returns a valid response 70% of the time. The real caller is a typed `HttpClient` (`IPartnerVerificationClient`).
- **Resilience** — Polly via `AddStandardResilienceHandler` (3 retries on timeouts/5xx). After retries are exhausted the incoming request still completes with `503` instead of crashing.
- **Messaging** — `ITransactionMessagePublisher` is the contract; `RabbitMqTransactionPublisher` is the RabbitMQ implementation. The service depends only on the interface so tests can substitute an in-memory publisher.
- **Errors** — `IExceptionHandler` maps unhandled exceptions (including mock timeouts) to `{ error, message, traceId }`.
- **Security** — `POST /api/v1/partner/transactions` requires either `X-Api-Key` or a JWT issued with `Auth:Jwt`. The mock verification endpoint is anonymous.

```
Client → API (auth + validation)
       → Partner Verification (HttpClient + Polly)
       → RabbitMQ queue partner.transactions
```

## Run the project

### Docker (API + queue)

```bash
docker compose up --build
```

- API / Swagger: http://localhost:8080/swagger
- Health: http://localhost:8080/health
- RabbitMQ UI: http://localhost:15672 (`guest` / `guest`)

```http
POST http://localhost:8080/api/v1/partner/transactions
X-Api-Key: local-dev-api-key
Content-Type: application/json

{
  "partnerId": "P-1001",
  "transactionReference": "TXN-99823",
  "amount": 250.00,
  "currency": "USD",
  "timestamp": "2024-05-10T14:30:00Z"
}
```

Expect `202 Accepted` when verification succeeds. Retry if the mock times out (Polly will retry internally; persistent upstream failure returns `503`).

### Local (without Docker)

1. Run RabbitMQ on `localhost:5672` (or `docker compose up rabbitmq`).
2. Start the API:

```bash
dotnet run --project src/PartnerTransactions.Api --launch-profile http
```

Swagger: http://localhost:5236/swagger  
#### Default API key: `local-dev-api-key`

## Run the tests

```bash
dotnet test PartnerTransactions.sln
```

With coverage:

```bash
dotnet test PartnerTransactions.sln /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

Tests cover FluentValidation, the submit orchestration, Polly retries (fake `HttpMessageHandler`), the mock timeout endpoint, the global exception handler, and API auth via `WebApplicationFactory`.
