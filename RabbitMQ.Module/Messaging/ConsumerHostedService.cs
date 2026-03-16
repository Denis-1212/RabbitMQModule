namespace RabbitMQ.Module.Messaging;

using Client;
using Client.Events;

using Configuration;

using Infrastructure;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Registration;

public class ConsumerHostedService(
    IConnectionManager connectionManager,
    IConsumerRegistry registry,
    MessageDispatcher dispatcher,
    MessagingOptions options,
    ILogger<ConsumerHostedService> logger)
    : BackgroundService
{

    #region Fields

    private readonly IConnectionManager _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
    private readonly IConsumerRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly MessageDispatcher _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    private readonly MessagingOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<ConsumerHostedService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly List<ConsumerInfo> _consumers = new();
    private IChannel? _controlChannel;
    private IConnection? _connection;
    private readonly SemaphoreSlim _startupLock = new(1, 1);
    private bool _isStarted;
    private readonly CancellationTokenSource _stopCts = new();

    #endregion

    #region Methods

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Остановка сервиса потребителей по команде");
        await _stopCts.CancelAsync();
        await StopConsumersAsync();
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _stopCts.Dispose();
        _startupLock.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _stopCts.Token);
        CancellationToken combinedToken = cts.Token;

        while (!combinedToken.IsCancellationRequested)
        {
            try
            {
                if (!_registry.HasRegistrations)
                {
                    _logger.LogInformation("Нет зарегистрированных потребителей, сервис не запущен");
                    await Task.Delay(TimeSpan.FromSeconds(5), combinedToken);
                    continue;
                }

                await EnsureConsumersAsync(combinedToken);

                await Task.Delay(Timeout.Infinite, combinedToken);
            }
            catch (OperationCanceledException) when (combinedToken.IsCancellationRequested)
            {
                _logger.LogInformation("Сервис потребителей остановлен");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Критическая ошибка в сервисе потребителей, перезапуск через 5 секунд");
                await Task.Delay(TimeSpan.FromSeconds(5), combinedToken);
                await StopConsumersAsync();
            }
        }
    }

    private async Task EnsureConsumersAsync(CancellationToken cancellationToken)
    {
        if (_isStarted && _connection?.IsOpen == true && _consumers.All(c => c.IsActive))
        {
            return;
        }

        await _startupLock.WaitAsync(cancellationToken);

        try
        {
            if (_isStarted && _connection?.IsOpen == true && _consumers.All(c => c.IsActive))
            {
                return;
            }

            await StopConsumersAsync();
            await StartConsumersAsync(cancellationToken);

            _isStarted = true;
        }
        finally
        {
            _startupLock.Release();
        }
    }

    private async Task StartConsumersAsync(CancellationToken cancellationToken)
    {
        _connection = await _connectionManager.GetConnectionAsync(cancellationToken);

        _connection.ConnectionShutdownAsync += HandleConnectionShutdownAsync;

        _controlChannel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        IEnumerable<ConsumerRegistration> registrations = _registry.GetAllRegistrations();

        foreach (ConsumerRegistration registration in registrations)
        {
            await StartConsumerAsync(registration, cancellationToken);
        }

        _logger.LogInformation("Запущено {Count} потребителей", _consumers.Count);
    }

    private async Task StartConsumerAsync(ConsumerRegistration registration, CancellationToken cancellationToken)
    {
        try
        {
            IChannel channel = await _connection!.CreateChannelAsync(cancellationToken: cancellationToken);

            await channel.BasicQosAsync(
                0,
                registration.Options.PrefetchCount,
                false,
                cancellationToken);

            string queueName = await DeclareQueueAsync(channel, registration, cancellationToken);

            var consumer = new AsyncEventingBasicConsumer(channel);

            // Создаем делегат правильного типа AsyncEventHandler<BasicDeliverEventArgs>
            AsyncEventHandler<BasicDeliverEventArgs> messageHandler = async (sender, args) =>
            {
                try
                {
                    await _dispatcher.DispatchAsync(channel, args, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Необработанная ошибка при обработке сообщения");

                    try
                    {
                        await channel.BasicNackAsync(args.DeliveryTag, false, false, cancellationToken);
                    }
                    catch (Exception nackEx)
                    {
                        _logger.LogError(nackEx, "Ошибка при NACK сообщения");
                    }
                }
            };

            consumer.ReceivedAsync += messageHandler;

            string consumerTag = await channel.BasicConsumeAsync(
                                     queueName,
                                     false,
                                     consumer,
                                     cancellationToken);

            var consumerInfo = new ConsumerInfo
            {
                Channel = channel,
                Consumer = consumer,
                QueueName = queueName,
                ConsumerTag = consumerTag,
                Registration = registration,
                MessageHandler = messageHandler // Сохраняем делегат правильного типа
            };

            _consumers.Add(consumerInfo);

            _logger.LogInformation(
                "Запущен потребитель для очереди {Queue}, тег {ConsumerTag}, тип сообщения {MessageType}",
                queueName,
                consumerTag,
                registration.MessageType.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Ошибка при запуске потребителя для {MessageType}",
                registration.MessageType.Name);

            throw;
        }
    }

    private async Task<string> DeclareQueueAsync(
        IChannel channel,
        ConsumerRegistration registration,
        CancellationToken cancellationToken)
    {
        ConsumerOptions options = registration.Options;

        string queueName = string.IsNullOrEmpty(_options.QueuePrefix)
                               ? options.QueueName
                               : $"{_options.QueuePrefix}.{options.QueueName}";

        Dictionary<string, object>? arguments = options.CreateQueueArguments();

        // Конвертируем Dictionary<string, object> в IDictionary<string, object?>?
        IDictionary<string, object?>? queueArgs = null;

        if (arguments != null)
        {
            queueArgs = arguments.ToDictionary(
                kvp => kvp.Key,
                kvp => (object?)kvp.Value);
        }

        QueueDeclareOk declareResult = await channel.QueueDeclareAsync(
                                           queueName,
                                           options.Durable,
                                           options.Exclusive,
                                           options.AutoDelete,
                                           queueArgs, // Теперь правильный тип
                                           cancellationToken: cancellationToken);

        if (!string.IsNullOrEmpty(options.ExchangeName))
        {
            await channel.QueueBindAsync(
                declareResult.QueueName,
                options.ExchangeName,
                options.RoutingKey ?? "",
                cancellationToken: cancellationToken);

            _logger.LogDebug(
                "Очередь {Queue} привязана к exchange {Exchange} с routing key {RoutingKey}",
                declareResult.QueueName,
                options.ExchangeName,
                options.RoutingKey);
        }

        return declareResult.QueueName;
    }

    private Task HandleConnectionShutdownAsync(object sender, ShutdownEventArgs e)
    {
        if (e.Initiator != ShutdownInitiator.Application)
        {
            _logger.LogWarning(
                "Соединение потеряно, инициатор: {Initiator}, причина: {Reason}",
                e.Initiator,
                e.ReplyText);

            _ = Task.Run(async () => await EnsureConsumersAsync(CancellationToken.None));
        }

        return Task.CompletedTask;
    }

    private async Task StopConsumersAsync()
    {
        if (!_consumers.Any())
        {
            return;
        }

        _logger.LogInformation("Остановка {Count} потребителей", _consumers.Count);

        foreach (ConsumerInfo consumer in _consumers)
        {
            try
            {
                if (consumer.Consumer != null && consumer.MessageHandler != null)
                {
                    consumer.Consumer.ReceivedAsync -= consumer.MessageHandler;
                }

                if (consumer.Channel != null)
                {
                    if (consumer.Channel.IsOpen)
                    {
                        await consumer.Channel.CloseAsync();
                    }

                    await consumer.Channel.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Ошибка при закрытии канала потребителя {ConsumerTag}", consumer.ConsumerTag);
            }
        }

        _consumers.Clear();

        if (_controlChannel != null)
        {
            try
            {
                if (_controlChannel.IsOpen)
                {
                    await _controlChannel.CloseAsync();
                }

                await _controlChannel.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Ошибка при закрытии контрольного канала");
            }

            _controlChannel = null;
        }

        if (_connection != null)
        {
            _connection.ConnectionShutdownAsync -= HandleConnectionShutdownAsync;
            _connection = null;
        }

        _isStarted = false;
    }

    #endregion

    #region Classes

    private class ConsumerInfo
    {

        #region Properties

        public IChannel? Channel { get; init; }
        public AsyncEventingBasicConsumer? Consumer { get; init; }
        public string QueueName { get; init; } = string.Empty;
        public string ConsumerTag { get; init; } = string.Empty;
        public ConsumerRegistration? Registration { get; init; }
        public AsyncEventHandler<BasicDeliverEventArgs>? MessageHandler { get; init; } // Правильный тип
        public bool IsActive => Channel?.IsOpen == true;

        #endregion

    }

    #endregion

}
