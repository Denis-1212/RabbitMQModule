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

    public bool LastPublishWasConfirmed { get; private set; }

    public TimeSpan? LastConfirmLatency =>
        _lastConfirmTime.HasValue && _lastPublishTime.HasValue
            ? _lastConfirmTime - _lastPublishTime
            : null;

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
                (exception, timeSpan, retryCount, context) =>
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

    public async Task PublishAsync<T>(
        T message,
        Action<IPublishConfiguration>? configure = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var config = new PublishConfiguration();
        configure?.Invoke(config);

        await _retryPolicy.ExecuteAsync(async () =>
        {
            IChannel channel = await _channelPool.GetAsync(cancellationToken);

            try
            {
                var envelope = MessageEnvelope.Create(message, _serializer, config.MessageId);

                LastMessageId = envelope.MessageId;
                _lastPublishTime = DateTime.UtcNow; //Запоминаем время отправки
                LastPublishWasConfirmed = false;

                BasicProperties props = config.CreateBasicProperties();
                props.Type = envelope.MessageType;
                props.MessageId = envelope.MessageId;

                _logger.LogDebug(
                    "Публикация сообщения {MessageId} типа {MessageType}",
                    envelope.MessageId,
                    envelope.MessageType);

                // Sequence number до публикации
                ulong seqBefore = await channel.GetNextPublishSequenceNumberAsync(cancellationToken);
                _logger.LogInformation("Sequence number ДО BasicPublishAsync: {SeqNo}", seqBefore);

                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                AsyncEventHandler<BasicAckEventArgs>? ackHandler = null;
                AsyncEventHandler<BasicNackEventArgs>? nackHandler = null;

                ackHandler = async (sender, args) =>
                {
                    _logger.LogInformation(
                        "ACK получен для сообщения {MessageId}, DeliveryTag: {DeliveryTag}",
                        envelope.MessageId,
                        args.DeliveryTag);

                    channel.BasicAcksAsync -= ackHandler;
                    channel.BasicNacksAsync -= nackHandler;

                    _lastConfirmTime = DateTime.UtcNow;
                    LastPublishWasConfirmed = true;

                    tcs.TrySetResult(true);
                    await Task.CompletedTask;
                };

                nackHandler = async (sender, args) =>
                {
                    _logger.LogWarning(
                        "❌ NACK получен для сообщения {MessageId}, DeliveryTag: {DeliveryTag}",
                        envelope.MessageId,
                        args.DeliveryTag);

                    channel.BasicAcksAsync -= ackHandler;
                    channel.BasicNacksAsync -= nackHandler;

                    var exception = new DeliveryFailureException($"Сообщение {envelope.MessageId} не подтверждено брокером");
                    tcs.TrySetException(exception);
                    await Task.CompletedTask;
                };

                channel.BasicAcksAsync += ackHandler;
                channel.BasicNacksAsync += nackHandler;
                byte[] body = _serializer.Serialize(envelope);

                try
                {
                    // ПУБЛИКУЕМ СООБЩЕНИЕ
                    await channel.BasicPublishAsync(
                        config.Exchange,
                        config.RoutingKey,
                        config.Mandatory,
                        props,
                        body,
                        cancellationToken);

                    ulong seqAfter = await channel.GetNextPublishSequenceNumberAsync(cancellationToken);
                    _logger.LogInformation("Sequence number ПОСЛЕ BasicPublishAsync: {SeqNo}", seqAfter);

                    // ЖДЕМ ПОДТВЕРЖДЕНИЕ ЕСЛИ НУЖНО
                    if (_deliveryControl.PublisherConfirmsEnabled && config.UsePublisherConfirms)
                    {
                        using var cts = new CancellationTokenSource(_deliveryControl.PublishConfirmationTimeoutMs);
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                            cancellationToken,
                            cts.Token);

                        using CancellationTokenRegistration registration = linkedCts.Token.Register(() =>
                        {
                            _logger.LogError("ТАЙМАУТ! Сообщение {MessageId} не подтверждено", envelope.MessageId);
                            tcs.TrySetException(
                                new TimeoutException(
                                    $"Таймаут ожидания подтверждения для сообщения {envelope.MessageId} " +
                                    $"({_deliveryControl.PublishConfirmationTimeoutMs} мс)"));
                        });

                        await tcs.Task;
                        _logger.LogTrace("Сообщение {MessageId} подтверждено брокером", envelope.MessageId);
                    }
                }
                finally
                {
                    channel.BasicAcksAsync -= ackHandler;
                    channel.BasicNacksAsync -= nackHandler;
                }
            }
            finally
            {
                await _channelPool.ReturnAsync(channel);
            }
        });
    }

    public void ResetStats()
    {
        LastPublishWasConfirmed = false;
        _lastPublishTime = null;
        _lastConfirmTime = null;
        LastMessageId = null;
    }

    private async Task WaitForConfirmationAsync(
        IChannel channel,
        string messageId,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        AsyncEventHandler<BasicAckEventArgs>? ackHandler = null;
        AsyncEventHandler<BasicNackEventArgs>? nackHandler = null;

        ackHandler = async (_, args) =>
        {
            _logger.LogInformation(
                "ACK получен для сообщения {MessageId}, DeliveryTag: {DeliveryTag}",
                messageId,
                args.DeliveryTag);

            try
            {
                channel.BasicAcksAsync -= ackHandler;
                channel.BasicNacksAsync -= nackHandler;
                tcs.TrySetResult(true);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в обработчике подтверждения");
            }
        };

        nackHandler = async (_, args) =>
        {
            _logger.LogWarning(
                "❌ NACK получен для сообщения {MessageId}, DeliveryTag: {DeliveryTag}",
                messageId,
                args.DeliveryTag);

            try
            {
                channel.BasicAcksAsync -= ackHandler;
                channel.BasicNacksAsync -= nackHandler;

                var exception = new DeliveryFailureException($"Сообщение {messageId} не подтверждено брокером")
                {
                    MessageId = messageId,
                    Reason = $"DeliveryTag: {args.DeliveryTag}, Multiple: {args.Multiple}"
                };

                tcs.TrySetException(exception);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в обработчике отсутствия подтверждения");
            }
        };

        channel.BasicAcksAsync += ackHandler;
        channel.BasicNacksAsync += nackHandler;

        try
        {
            using var cts = new CancellationTokenSource(_deliveryControl.PublishConfirmationTimeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                cts.Token);

            using CancellationTokenRegistration registration = linkedCts.Token.Register(async () =>
            {
                // ДИАГНОСТИКА 2: Проверяем счетчик после таймаута
                ulong seqNoAfter = await channel.GetNextPublishSequenceNumberAsync(cancellationToken);
                _logger.LogError(
                    "ТАЙМАУТ! Сообщение {MessageId} не подтверждено. " +
                    "Sequence number ПОСЛЕ: {SeqNo}",
                    messageId,
                    seqNoAfter); // БЫЛО: после таймаута: 

                channel.BasicAcksAsync -= ackHandler;
                channel.BasicNacksAsync -= nackHandler;

                var timeoutException = new TimeoutException(
                    $"Таймаут ожидания подтверждения для сообщения {messageId} ({_deliveryControl.PublishConfirmationTimeoutMs} мс)");

                tcs.TrySetException(timeoutException);
            });

            await tcs.Task;
        }
        finally
        {
            channel.BasicAcksAsync -= ackHandler;
            channel.BasicNacksAsync -= nackHandler;
        }
    }

    #endregion

}
