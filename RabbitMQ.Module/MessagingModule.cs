namespace RabbitMQ.Module;

using Configuration;

using Contracts;

using DeliveryControl;

using Infrastructure;
using Infrastructure.Serialization;

using Messaging;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Registration;

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

        ConnectionManager = new ConnectionManager(Options, LoggerFactory!.CreateLogger<ConnectionManager>());
        _channelPool = new ChannelPool(ConnectionManager, Options, LoggerFactory!.CreateLogger<ChannelPool>());

        _serializer = new NewtonsoftJsonSerializer();
        Registry = new ConsumerRegistry();
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
        IPublisher publisher = CreatePublisher(); // Для повторной публикации при retry

        return new MessageDispatcher(
            Registry,
            _serializer,
            logger,
            Options,
            metrics,
            ServiceProvider);
    }

    #endregion

}
