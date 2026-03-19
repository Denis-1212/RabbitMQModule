namespace RabbitMQ.Module.Messaging;

using Client;
using Client.Events;
using Client.Exceptions;

using Configuration;

using Contracts;

using DeliveryControl;

using Infrastructure;
using Infrastructure.Serialization;

using Microsoft.Extensions.Logging;

using Polly;
using Polly.Retry;

/// <summary>
/// Реализация издателя сообщений в RabbitMQ
/// </summary>
public class Publisher : IPublisher
{

    #region Fields

    private readonly IChannelPool _channelPool;
    private readonly IMessageSerializer _serializer;
    private readonly ILogger<Publisher> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly DeliveryControlOptions _deliveryControl;
    private DateTime? _lastPublishTime;
    private DateTime? _lastConfirmTime;

    #endregion

    #region Properties

    /// <inheritdoc/>
    public bool LastPublishWasConfirmed { get; private set; }

    /// <inheritdoc/>
    public TimeSpan? LastConfirmLatency =>
        _lastConfirmTime.HasValue && _lastPublishTime.HasValue
            ? _lastConfirmTime - _lastPublishTime
            : null;

    /// <inheritdoc/>
    public string? LastMessageId { get; private set; }

    #endregion

    #region Constructors

    public Publisher(
        IChannelPool channelPool,
        IMessageSerializer serializer,
        ILogger<Publisher> logger,
        DeliveryControlOptions deliveryControl)
    {
        _channelPool = channelPool ?? throw new ArgumentNullException(nameof(channelPool));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _deliveryControl = deliveryControl ?? throw new ArgumentNullException(nameof(deliveryControl));

        _retryPolicy = Policy
            .Handle<AlreadyClosedException>()
            .Or<OperationInterruptedException>()
            .Or<BrokerUnreachableException>()
            .Or<DeliveryFailureException>()
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, _) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Ошибка публикации. Попытка {RetryCount} через {Delay}мс",
                        retryCount,
                        timeSpan.TotalMilliseconds);
                });
    }

    #endregion

    #region Methods

    public async Task PublishAsync(
        MessageEnvelope envelope,
        Action<IPublishConfiguration>? configure = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        _logger.LogCritical(
            "🔥🔥🔥 PUBLISH ENVELOPE CALLED with Id: {MessageId}, Type: {MessageType}",
            envelope.MessageId,
            envelope.MessageType);

        var config = new PublishConfiguration();
        configure?.Invoke(config);

        // СОХРАНЯЕМ ОРИГИНАЛЬНЫЙ MESSAGEID
        string originalMessageId = envelope.MessageId;

        await _retryPolicy.ExecuteAsync(async () =>
        {
            IChannel channel = await _channelPool.GetAsync(cancellationToken);

            try
            {
                BasicProperties props = config.CreateBasicProperties();
                props.Type = envelope.MessageType;
                props.MessageId = originalMessageId; // ✅ ИСПОЛЬЗУЕМ ОРИГИНАЛЬНЫЙ ID
                props.Headers ??= new Dictionary<string, object?>();

                // Добавляем информацию о повторной попытке
                props.Headers["x-retry-attempt"] = envelope.RetryAttempt;
                props.Headers["x-original-message-id"] = originalMessageId;

                // Копируем остальные заголовки
                if (envelope.Headers != null)
                {
                    foreach (KeyValuePair<string, object> header in envelope.Headers)
                    {
                        if (!header.Key.StartsWith("x-")) // Не перезаписываем наши системные
                        {
                            props.Headers[header.Key] = header.Value;
                        }
                    }
                }

                _logger.LogDebug(
                    "Повторная публикация конверта {MessageId} (оригинал {OriginalId}) типа {MessageType}, попытка {RetryAttempt}",
                    envelope.MessageId,
                    originalMessageId,
                    envelope.MessageType,
                    envelope.RetryAttempt);

                byte[] fullEnvelopeBytes = _serializer.Serialize(envelope);
                await PublishWithConfirmsAsync(
                    channel,
                    config.Exchange,
                    config.RoutingKey,
                    config.Mandatory,
                    props,
                    fullEnvelopeBytes,
                    originalMessageId, // Ждем подтверждение для оригинального ID
                    cancellationToken);
            }
            finally
            {
                await _channelPool.ReturnAsync(channel);
            }
        });
    }

    public async Task PublishAsync<T>(
        T message,
        Action<IPublishConfiguration>? configure = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        // _logger.LogTrace("PUBLISH<T> CALLED for type {Type}", typeof(T).Name);
        var config = new PublishConfiguration();
        configure?.Invoke(config);

        await _retryPolicy.ExecuteAsync(async () =>
        {
            IChannel channel = await _channelPool.GetAsync(cancellationToken);

            try
            {
                var envelope = MessageEnvelope.Create(message, _serializer, config.MessageId);

                LastMessageId = envelope.MessageId;
                _lastPublishTime = DateTime.UtcNow;
                LastPublishWasConfirmed = false;

                BasicProperties props = config.CreateBasicProperties();
                props.Type = envelope.MessageType;
                props.MessageId = envelope.MessageId;

                _logger.LogDebug(
                    "Публикация сообщения {MessageId} типа {MessageType}",
                    envelope.MessageId,
                    envelope.MessageType);

                // Получаем sequence number до публикации (для диагностики)
                ulong seqBefore = await channel.GetNextPublishSequenceNumberAsync(cancellationToken);
                _logger.LogInformation("Sequence number ДО BasicPublishAsync: {SeqNo}", seqBefore);
                byte[] fullEnvelopeBytes = _serializer.Serialize(envelope);
                await PublishWithConfirmsAsync(
                    channel,
                    config.Exchange,
                    config.RoutingKey,
                    config.Mandatory,
                    props,
                    fullEnvelopeBytes,
                    envelope.MessageId,
                    cancellationToken);

                // Получаем sequence number после публикации (для диагностики)
                ulong seqAfter = await channel.GetNextPublishSequenceNumberAsync(cancellationToken);
                _logger.LogInformation("Sequence number ПОСЛЕ BasicPublishAsync: {SeqNo}", seqAfter);
            }
            finally
            {
                await _channelPool.ReturnAsync(channel);
            }
        });
    }

    /// <inheritdoc/>
    public void ResetStats()
    {
        LastPublishWasConfirmed = false;
        _lastPublishTime = null;
        _lastConfirmTime = null;
        LastMessageId = null;
    }

    /// <summary>
    /// Публикует сообщение с ожиданием подтверждения от брокера
    /// </summary>
    private async Task PublishWithConfirmsAsync(
        IChannel channel,
        string exchange,
        string routingKey,
        bool mandatory,
        BasicProperties properties,
        byte[] body,
        string messageId,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        AsyncEventHandler<BasicAckEventArgs>? ackHandler = null;
        AsyncEventHandler<BasicNackEventArgs>? nackHandler = null;

        ackHandler = async (sender, args) =>
        {
            _logger.LogDebug("Получено подтверждение (ACK) для сообщения {MessageId}", messageId);

            channel.BasicAcksAsync -= ackHandler;
            channel.BasicNacksAsync -= nackHandler;

            _lastConfirmTime = DateTime.UtcNow;
            LastPublishWasConfirmed = true;

            tcs.TrySetResult(true);
            await Task.CompletedTask;
        };

        nackHandler = async (sender, args) =>
        {
            _logger.LogWarning("Получен отказ (NACK) для сообщения {MessageId}", messageId);

            channel.BasicAcksAsync -= ackHandler;
            channel.BasicNacksAsync -= nackHandler;

            var exception = new DeliveryFailureException($"Сообщение {messageId} не подтверждено брокером");
            tcs.TrySetException(exception);
            await Task.CompletedTask;
        };

        channel.BasicAcksAsync += ackHandler;
        channel.BasicNacksAsync += nackHandler;

        try
        {
            _logger.LogTrace(
                "Отправка сообщения {MessageId} в exchange '{Exchange}' с routing key '{RoutingKey}'",
                messageId,
                exchange,
                routingKey);

            await channel.BasicPublishAsync(
                exchange,
                routingKey,
                mandatory,
                properties,
                body,
                cancellationToken);

            if (_deliveryControl.PublisherConfirmsEnabled)
            {
                _logger.LogDebug(
                    "Ожидание подтверждения для сообщения {MessageId} (таймаут: {Timeout} мс)",
                    messageId,
                    _deliveryControl.PublishConfirmationTimeoutMs);

                using var cts = new CancellationTokenSource(_deliveryControl.PublishConfirmationTimeoutMs);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    cts.Token);

                using CancellationTokenRegistration registration = linkedCts.Token.Register(() =>
                {
                    var timeoutException = new TimeoutException(
                        $"Таймаут ожидания подтверждения для сообщения {messageId} " +
                        $"({_deliveryControl.PublishConfirmationTimeoutMs} мс)");

                    _logger.LogError(timeoutException, "Таймаут подтверждения для сообщения {MessageId}", messageId);

                    tcs.TrySetException(timeoutException);
                });

                await tcs.Task;
                _logger.LogDebug("Сообщение {MessageId} подтверждено брокером", messageId);
            }
            else
            {
                _logger.LogTrace("Сообщение {MessageId} опубликовано без ожидания подтверждения", messageId);
            }
        }
        finally
        {
            channel.BasicAcksAsync -= ackHandler;
            channel.BasicNacksAsync -= nackHandler;
        }
    }

    #endregion

}
