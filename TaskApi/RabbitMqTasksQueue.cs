using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace TasksDemo.Api;

public sealed class RabbitMqTasksQueue(
    IConfiguration configuration,
    ILogger<RabbitMqTasksQueue> logger
) : ITasksQueue, IAsyncDisposable
{
    readonly object _locker = new();
    readonly CancellationTokenSource _cts = new();

    IConnection? _connection;
    IChannel? _channel;
    Task? _starting;
    bool _disposed;

    public async Task PublishTaskCompletedAsync(TaskItem task, CancellationToken cancellationToken)
    {
        await EnsureStartedAsync();

        if (_channel is null || _connection is null || !_connection.IsOpen)
        {
            logger.LogWarning("RabbitMQ unavailable — skip publish for task {TaskId}", task.Id);
            return;
        }

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                _cts.Token,
                cancellationToken
            );

            var msg = new
            {
                taskId = task.Id,
                title = task.Title,
                completedAt = task.CompletedAt?.UtcDateTime,
                priority = task.Priority.ToString(),
            };

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg));

            await _channel.BasicPublishAsync(
                exchange: "task.events",
                routingKey: "task.completed",
                body: body,
                cancellationToken: linked.Token
            );

            logger.LogInformation("Published task.completed for task {TaskId}", task.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish task.completed for task {TaskId}", task.Id);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_disposed)
            return;

        _disposed = true;

        if (_channel?.IsOpen is true)
        {
            _channel.Dispose();
        }

        if (_connection?.IsOpen is true)
        {
            _connection.Dispose();
        }
    }

    Task EnsureStartedAsync()
    {
        lock (_locker)
        {
            if (_starting is not null)
                return _starting;

            _starting = Task.Run(async () =>
            {
                var factory = new ConnectionFactory
                {
                    HostName = configuration.GetValue("RabbitMQ:HostName", "localhost"),
                    Port = configuration.GetValue("RabbitMQ:Port", 5672),
                    UserName = configuration.GetValue("RabbitMQ:UserName", "guest"),
                    Password = configuration.GetValue("RabbitMQ:Password", "guest"),
                    AutomaticRecoveryEnabled = true,
                };

                _connection = await factory.CreateConnectionAsync(_cts.Token);
                _channel = await _connection.CreateChannelAsync(cancellationToken: _cts.Token);

                await _channel.ExchangeDeclareAsync(
                    exchange: "task.events",
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false,
                    cancellationToken: _cts.Token
                );

                logger.LogInformation(
                    "RabbitMQ connected to {Host}:{Port}",
                    configuration.GetValue("RabbitMQ:HostName", "localhost"),
                    configuration.GetValue("RabbitMQ:Port", 5672)
                );
            });

            return _starting;
        }
    }
}
