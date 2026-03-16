namespace RabbitMQ.Module.Infrastructure;

using Client;
using Client.Events;
using Client.Exceptions;

using Configuration;

using Microsoft.Extensions.Logging;

using Polly;
using Polly.Retry;

public class ConnectionManager : IConnectionManager
{

    #region Fields

    private readonly ILogger<ConnectionManager> _logger;
    private readonly ConnectionFactory _factory;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly SemaphoreSlim _connectionLock;
    private IConnection? _connection;
    private bool _disposed;

    #endregion

    #region Constructors

    public ConnectionManager(MessagingOptions options, ILogger<ConnectionManager> logger)
    {
        _ = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _factory = options.CreateConnectionFactory();
        _connectionLock = new SemaphoreSlim(1, 1);

        _retryPolicy = Policy
            .Handle<BrokerUnreachableException>()
            .Or<OperationInterruptedException>()
            .WaitAndRetryAsync(
                options.ConnectionRetryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Error connecting to RabbitMQ. Attempt {RetryCount} of {MaxRetries} in {Delay} ms",
                        retryCount,
                        options.ConnectionRetryCount,
                        timeSpan.TotalMilliseconds);
                });
    }

    #endregion

    #region Methods

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection?.IsOpen == true)
        {
            return _connection;
        }

        await _connectionLock.WaitAsync(cancellationToken);

        try
        {
            if (_connection?.IsOpen == true)
            {
                return _connection;
            }

            _connection = await _retryPolicy.ExecuteAsync(async () =>
            {
                _logger.LogInformation("Establishing a connection to RabbitMQ: {Host}", _factory.Uri.Host);
                IConnection connection = await _factory.CreateConnectionAsync(cancellationToken);

                connection.ConnectionShutdownAsync += HandleConnectionShutdownAsync;
                connection.ConnectionBlockedAsync += HandleConnectionBlockedAsync;
                connection.ConnectionUnblockedAsync += HandleConnectionUnblockedAsync;

                _logger.LogInformation("Connection to RabbitMQ established successfully");
                return connection;
            });

            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_connection != null)
        {
            _connection.ConnectionShutdownAsync -= HandleConnectionShutdownAsync;
            _connection.ConnectionBlockedAsync -= HandleConnectionBlockedAsync;
            _connection.ConnectionUnblockedAsync -= HandleConnectionUnblockedAsync;

            try
            {
                await _connection.CloseAsync();
                await _connection.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing RabbitMQ connection");
            }
        }

        _connectionLock.Dispose();
    }

    private Task HandleConnectionShutdownAsync(object sender, ShutdownEventArgs e)
    {
        _logger.LogWarning("The connection to RabbitMQ was closed: {Reason}", e.ReplyText);

        if (!_disposed && e.Initiator != ShutdownInitiator.Application)
        {
            _ = Task.Run(async () => await ReconnectAsync());
        }

        return Task.CompletedTask;
    }

    private Task HandleConnectionBlockedAsync(object sender, ConnectionBlockedEventArgs e)
    {
        _logger.LogDebug("The connection to RabbitMQ is blocked: {Reason}", e.Reason);
        return Task.CompletedTask;
    }

    private Task HandleConnectionUnblockedAsync(object sender, AsyncEventArgs e)
    {
        _logger.LogDebug("The connection to RabbitMQ is unblocked");
        return Task.CompletedTask;
    }

    private async Task ReconnectAsync()
    {
        try
        {
            await _connectionLock.WaitAsync();

            if (_disposed)
            {
                return;
            }

            if (_connection != null)
            {
                _connection.ConnectionShutdownAsync -= HandleConnectionShutdownAsync;
                _connection.ConnectionBlockedAsync -= HandleConnectionBlockedAsync;
                _connection.ConnectionUnblockedAsync -= HandleConnectionUnblockedAsync;

                try
                {
                    await _connection.CloseAsync();
                    await _connection.DisposeAsync();
                }
                catch
                {
                    _logger.LogWarning("Error closing connection to RabbitMQ");
                }

                _connection = null;
            }

            _connection = await _retryPolicy.ExecuteAsync(async () =>
            {
                _logger.LogDebug("Reconnecting to RabbitMQ...");
                IConnection connection = await _factory.CreateConnectionAsync();

                connection.ConnectionShutdownAsync += HandleConnectionShutdownAsync;
                connection.ConnectionBlockedAsync += HandleConnectionBlockedAsync;
                connection.ConnectionUnblockedAsync += HandleConnectionUnblockedAsync;

                _logger.LogDebug("Reconnection successful");
                return connection;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error reconnecting to RabbitMQ");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    #endregion

}
