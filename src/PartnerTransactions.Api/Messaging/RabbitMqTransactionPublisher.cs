using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PartnerTransactions.Api.Models;
using PartnerTransactions.Api.Options;
using RabbitMQ.Client;

namespace PartnerTransactions.Api.Messaging;

[ExcludeFromCodeCoverage]
public sealed class RabbitMqTransactionPublisher : ITransactionMessagePublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqTransactionPublisher> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
    private IConnection? _connection;
    private IChannel? _channel;
    private bool _disposed;

    public RabbitMqTransactionPublisher(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqTransactionPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync(PartnerTransactionMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var channel = await GetChannelAsync(cancellationToken);
            var body = JsonSerializer.SerializeToUtf8Bytes(message, _serializerOptions);

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: _options.QueueName,
                body: body,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Published transaction {TransactionReference} for partner {PartnerId}",
                message.TransactionReference,
                message.PartnerId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new MessagePublishException("Failed to publish partner transaction to the queue.", ex);
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_channel is { IsOpen: true })
            {
                return _channel;
            }

            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await _channel.QueueDeclareAsync(
                queue: _options.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: cancellationToken);

            return _channel;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_channel is not null)
        {
            await _channel.CloseAsync();
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }

        _gate.Dispose();
    }
}
