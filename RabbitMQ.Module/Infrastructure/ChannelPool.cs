namespace RabbitMQ.Module.Infrastructure;

using System.Collections.Concurrent;

using Client;

using Configuration;

using Microsoft.Extensions.Logging;

public class ChannelPool : IChannelPool
{

    #region Fields

    private readonly IConnectionManager _connectionManager;
    private readonly MessagingOptions _options;
    private readonly ILogger<ChannelPool> _logger;
    private readonly ConcurrentQueue<IChannel> _channels = new();
    private readonly SemaphoreSlim _semaphore;
    private readonly CancellationTokenSource _disposeCts = new();

    private int _channelCount;
    private bool _disposed;

    #endregion

    #region Constructors

    public ChannelPool(
        IConnectionManager connectionManager,
        MessagingOptions options,
        ILogger<ChannelPool> logger)
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _semaphore = new SemaphoreSlim(_options.MaxChannelsInPool, _options.MaxChannelsInPool);
    }

    #endregion

    #region Methods

    public async Task<IChannel> GetAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _disposeCts.Token);

        try
        {
            await _semaphore.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            if (_disposeCts.Token.IsCancellationRequested)
            {
                throw new ObjectDisposedException(nameof(ChannelPool));
            }

            throw;
        }

        while (_channels.TryDequeue(out IChannel? channel))
        {
            if (channel.IsOpen)
            {
                _logger.LogTrace("Канал {ChannelNumber} получен из пула", channel.ChannelNumber);
                return channel;
            }

            await channel.DisposeAsync();
            Interlocked.Decrement(ref _channelCount);
            _logger.LogTrace("Закрытый канал удален из пула");
        }

        try
        {
            IConnection connection = await _connectionManager.GetConnectionAsync(cts.Token);

            DeliveryControlOptions deliveryControl = _options.DeliveryControl;

            var channelOptions = new CreateChannelOptions(
                deliveryControl.PublisherConfirmsEnabled,
                deliveryControl.PublisherConfirmationTrackingEnabled,
                null,
                null
            );

            IChannel newChannel = await connection.CreateChannelAsync(
                                      channelOptions,
                                      cts.Token);

            await newChannel.BasicQosAsync(
                0,
                1,
                false,
                cts.Token);

            int channelNumber = Interlocked.Increment(ref _channelCount);
            _logger.LogDebug(
                "Создан новый канал {ChannelNumber}. Всего каналов: {Total}. Режим подтверждений: {ConfirmMode}",
                newChannel.ChannelNumber,
                channelNumber,
                deliveryControl.PublisherConfirmsEnabled);

            return newChannel;
        }
        catch
        {
            _semaphore.Release();
            throw;
        }
    }

    public async Task ReturnAsync(IChannel channel)
    {
        if (_disposed || channel == null)
        {
            return;
        }

        try
        {
            if (channel.IsOpen)
            {
                _channels.Enqueue(channel);
                _logger.LogTrace("Канал {ChannelNumber} возвращен в пул", channel.ChannelNumber);
            }
            else
            {
                await channel.DisposeAsync();
                Interlocked.Decrement(ref _channelCount);
                _logger.LogTrace("Закрытый канал утилизирован при возврате");
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _disposeCts.CancelAsync();

        _logger.LogInformation("Очистка пула каналов. Текущих каналов: {Count}", _channelCount);

        var disposeTasks = new List<Task>();

        while (_channels.TryDequeue(out IChannel? channel))
        {
            disposeTasks.Add(channel.DisposeAsync().AsTask());
        }

        if (disposeTasks.Any())
        {
            await Task.WhenAll(disposeTasks);
        }

        _semaphore.Dispose();
        _disposeCts.Dispose();

        _logger.LogInformation("Пул каналов очищен");
    }

    #endregion

}
