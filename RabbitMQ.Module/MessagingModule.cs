namespace RabbitMQ.Module;

using Configuration;

using Contracts;

using Deduplication;

using DeliveryControl;

using Infrastructure;
using Infrastructure.Serialization;

using Messaging;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Registration;

using StackExchange.Redis;

/// <summary>
/// Основной модуль для работы с RabbitMQ
/// </summary>
public class MessagingModule : IAsyncDisposable
{

    #region Fields

    private readonly IChannelPool _channelPool;

    private ConsumerHostedService? _consumerService;

    private bool _disposed;
    private readonly NewtonsoftJsonSerializer _serializer;
    private IDeduplicationStore? _deduplicationStore;
    private readonly ILogger<MessagingModule> _logger;

    #endregion

    #region Properties

    internal IConnectionManager ConnectionManager { get; }

    internal IConsumerRegistry Registry { get; }

    internal IMessageSerializer Serializer => _serializer;

    internal ILoggerFactory LoggerFactory { get; }

    internal IServiceProvider? ServiceProvider { get; }

    internal MessagingOptions Options { get; }

    #endregion

    #region Constructors

    private MessagingModule(
        MessagingOptions options,
        ILoggerFactory? loggerFactory,
        IServiceProvider? serviceProvider = null)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        LoggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        ServiceProvider = serviceProvider;

        ConnectionManager = new ConnectionManager(Options, LoggerFactory.CreateLogger<ConnectionManager>());
        _channelPool = new ChannelPool(ConnectionManager, Options, LoggerFactory.CreateLogger<ChannelPool>());

        _serializer = new NewtonsoftJsonSerializer();
        Registry = new ConsumerRegistry();
        _logger = LoggerFactory.CreateLogger<MessagingModule>();
    }

    #endregion

    #region Methods

    /// <summary>
    /// Создает экземпляр модуля RabbitMQ
    /// </summary>
    /// <param name = "configure">Действие для настройки параметров</param>
    /// <param name = "loggerFactory">Фабрика логгеров (опционально)</param>
    /// <param name = "serviceProvider">Провайдер сервисов для DI (опционально)</param>
    /// <returns>Экземпляр модуля</returns>
    public static MessagingModule Create(
        Action<MessagingOptions> configure,
        ILoggerFactory? loggerFactory = null,
        IServiceProvider? serviceProvider = null)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new MessagingOptions();
        configure(options);
        options.Validate(); // Валидация параметров

        loggerFactory ??= NullLoggerFactory.Instance;

        return new MessagingModule(options, loggerFactory, serviceProvider);
    }

    /// <summary>
    /// Регистрирует потребителя сообщений
    /// </summary>
    /// <typeparam name = "TMessage">Тип сообщения</typeparam>
    /// <typeparam name = "THandler">Тип обработчика</typeparam>
    /// <param name = "configure">Настройки потребителя</param>
    /// <returns>Экземпляр модуля для Fluent API</returns>
    public MessagingModule AddConsumer<TMessage, THandler>(
        Action<ConsumerOptions> configure)
        where THandler : class, IMessageHandler<TMessage>
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new ConsumerOptions();
        configure(options);
        options.Validate(); // Валидация параметров потребителя

        var registration = new ConsumerRegistration(
            typeof(TMessage),
            typeof(THandler),
            options);

        Registry.AddRegistration(registration);

        return this;
    }

    /// <summary>
    /// Создает издателя сообщений
    /// </summary>
    /// <returns>Интерфейс для публикации сообщений</returns>
    public IPublisher CreatePublisher()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ILogger<Publisher> logger = LoggerFactory.CreateLogger<Publisher>();
        return new Publisher(_channelPool, _serializer, logger, Options.DeliveryControl);
    }

    /// <summary>
    /// Запускает всех зарегистрированных потребителей
    /// </summary>
    /// <param name = "cancellationToken">Токен отмены</param>
    public async Task StartConsumersAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_consumerService != null)
        {
            return;
        }

        await InitializeDeduplicationStoreAsync();
        MessageDispatcher dispatcher = CreateDispatcher();
        _consumerService = CreateConsumerHostedService(dispatcher);
        await _consumerService.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Освобождает ресурсы модуля
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopConsumersAsync();

        await _channelPool.DisposeAsync();
        await ConnectionManager.DisposeAsync();

        _disposed = true;
    }

    /// <summary>
    /// Останавливает всех потребителей
    /// </summary>
    /// <param name = "cancellationToken">Токен отмены</param>
    public async Task StopConsumersAsync(CancellationToken cancellationToken = default)
    {
        if (_consumerService != null)
        {
            await _consumerService.StopAsync(cancellationToken);
            _consumerService = null;
        }
    }

    private async Task InitializeDeduplicationStoreAsync()
    {
        if (Options.Deduplication.StoreType == DeduplicationStoreType.None)
        {
            _deduplicationStore = null;
            return;
        }

        ILogger<DefaultDeliveryMetrics> metricsLogger = LoggerFactory.CreateLogger<DefaultDeliveryMetrics>();
        var metrics = new DefaultDeliveryMetrics(metricsLogger);

        if (Options.Deduplication.StoreType == DeduplicationStoreType.InMemory)
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            _deduplicationStore = new InMemoryDeduplicationStore(
                cache,
                LoggerFactory.CreateLogger<InMemoryDeduplicationStore>(),
                Options.Deduplication,
                metrics);

            return;
        }

        if (Options.Deduplication.StoreType == DeduplicationStoreType.Redis)
        {
            try
            {
                ConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(Options.Deduplication.RedisConnectionString!);

                // Создаем fallback store если включено
                IDeduplicationStore? fallback = null;

                if (Options.Deduplication.EnableRedisFallback)
                {
                    var cache = new MemoryCache(new MemoryCacheOptions());
                    fallback = new InMemoryDeduplicationStore(
                        cache,
                        LoggerFactory.CreateLogger<InMemoryDeduplicationStore>(),
                        Options.Deduplication,
                        metrics);

                    _logger.LogInformation("Redis fallback to In-Memory enabled");
                }

                _deduplicationStore = new RedisDeduplicationStore(
                    redis,
                    LoggerFactory.CreateLogger<RedisDeduplicationStore>(),
                    Options.Deduplication,
                    fallback,
                    metrics);

                _logger.LogInformation("Redis deduplication store initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Redis deduplication store");

                if (Options.Deduplication.EnableRedisFallback)
                {
                    _logger.LogWarning("Falling back to In-Memory deduplication store");
                    var cache = new MemoryCache(new MemoryCacheOptions());
                    _deduplicationStore = new InMemoryDeduplicationStore(
                        cache,
                        LoggerFactory.CreateLogger<InMemoryDeduplicationStore>(),
                        Options.Deduplication,
                        metrics);
                }
                else
                {
                    throw;
                }
            }
        }

        await Task.CompletedTask;
    }

    private ConsumerHostedService CreateConsumerHostedService(MessageDispatcher dispatcher)
    {
        return new ConsumerHostedService(
            ConnectionManager,
            Registry,
            dispatcher,
            Options,
            LoggerFactory.CreateLogger<ConsumerHostedService>());
    }

    private MessageDispatcher CreateDispatcher()
    {
        ILogger<MessageDispatcher> logger = LoggerFactory.CreateLogger<MessageDispatcher>();
        ILogger<DefaultDeliveryMetrics> metricsLogger = LoggerFactory.CreateLogger<DefaultDeliveryMetrics>();
        var metrics = new DefaultDeliveryMetrics(metricsLogger);
        // IPublisher publisher = CreatePublisher(); // Для повторной публикации при retry

        return new MessageDispatcher(
            Registry,
            _serializer,
            logger,
            Options,
            metrics,
            ServiceProvider,
            _deduplicationStore,
            Options.Deduplication);
    }

    #endregion

    // private readonly ILogger<MessagingModule> _logger;
}
