namespace RabbitMQ.Module.Messaging;

using System.Reflection;

using Client;
using Client.Events;

using Configuration;

using Contracts;

using Deduplication;

using Infrastructure.Serialization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Registration;

public class MessageDispatcher(
    IConsumerRegistry registry,
    IMessageSerializer serializer,
    ILogger<MessageDispatcher> logger,
    MessagingOptions options,
    IDeliveryMetrics metrics,
    IServiceProvider? serviceProvider = null,
    IDeduplicationStore? deduplicationStore = null,
    DeduplicationOptions? deduplicationOptions = null
)
{

    #region Fields

    private readonly IConsumerRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly IMessageSerializer _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    private readonly ILogger<MessageDispatcher> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly MessagingOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly IDeliveryMetrics _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));

    private readonly bool _deduplicationEnabled = deduplicationOptions is not { StoreType: DeduplicationStoreType.None }
                                                  && deduplicationStore != null;

    private readonly DeduplicationOptions _deduplicationOptions = deduplicationOptions ?? options.Deduplication;

    #endregion

    #region Methods

    public async Task DispatchAsync(
        IChannel channel,
        BasicDeliverEventArgs args,
        CancellationToken cancellationToken = default)
    {
        ulong deliveryTag = args.DeliveryTag;
        DateTime startTime = DateTime.UtcNow;

        try
        {
            // 1. Десериализация и валидация
            (MessageEnvelope? envelope, string? messageType) = await DeserializeAndValidateAsync(args, channel, cancellationToken);

            if (envelope == null)
            {
                return; // уже обработано
            }

            // 2. Дедубликация
            if (!await TryProcessDeduplicationAsync(envelope, messageType!, channel, deliveryTag, cancellationToken))
            {
                return; // дубликат
            }

            // 3. Получение обработчиков
            IReadOnlyList<ConsumerRegistration> handlers = GetHandlersForMessage(envelope);

            if (!handlers.Any())
            {
                await channel.BasicAckAsync(deliveryTag, false, cancellationToken);
                return;
            }

            // 4. Обработка сообщения
            await ProcessMessageWithHandlersAsync(
                envelope,
                messageType!,
                handlers,
                channel,
                args,
                deliveryTag,
                startTime,
                cancellationToken);
        }
        catch (Exception ex)
        {
            await HandleCriticalErrorAsync(channel, deliveryTag, ex, cancellationToken);
        }
    }

    private async Task<(MessageEnvelope? envelope, string? messageType)> DeserializeAndValidateAsync(
        BasicDeliverEventArgs args,
        IChannel channel,
        CancellationToken cancellationToken)
    {
        var envelope = _serializer.Deserialize<MessageEnvelope>(args.Body.ToArray());
        string messageType = envelope.MessageType;

        _logger.LogDebug(
            "Получено сообщение {MessageId} типа {MessageType}, попытка {RetryAttempt}",
            envelope.MessageId,
            envelope.MessageType,
            envelope.RetryAttempt);

        if (string.IsNullOrEmpty(envelope.MessageType))
        {
            _logger.LogError("MessageType пустой в полученном сообщении");
            await channel.BasicNackAsync(args.DeliveryTag, false, false, cancellationToken);
            return (null, null);
        }

        return (envelope, messageType);
    }

    private async Task<bool> TryProcessDeduplicationAsync(
        MessageEnvelope envelope,
        string messageType,
        IChannel channel,
        ulong deliveryTag,
        CancellationToken cancellationToken)
    {
        if (!_deduplicationEnabled)
        {
            return true;
        }

        bool isFirst = await deduplicationStore!.TryAddAsync(
                           envelope.MessageId,
                           _deduplicationOptions.Ttl,
                           cancellationToken);

        if (isFirst)
        {
            return true;
        }

        _logger.LogDebug("Дубликат сообщения {MessageId}, пропускаем обработку", envelope.MessageId);
        _metrics.MessageDeduplicated(messageType);
        await channel.BasicAckAsync(deliveryTag, false, cancellationToken);
        return false;
    }

    private IReadOnlyList<ConsumerRegistration> GetHandlersForMessage(MessageEnvelope envelope)
    {
        var messageTypeObj = Type.GetType(envelope.MessageType);

        if (messageTypeObj == null)
        {
            throw new InvalidOperationException($"Не удалось определить тип сообщения: {envelope.MessageType}");
        }

        return _registry.GetRegistrationsForMessage(messageTypeObj).ToList();
    }

    private async Task ProcessMessageWithHandlersAsync(
        MessageEnvelope envelope,
        string messageType,
        IReadOnlyList<ConsumerRegistration> handlers,
        IChannel channel,
        BasicDeliverEventArgs args,
        ulong deliveryTag,
        DateTime startTime,
        CancellationToken cancellationToken)
    {
        object message = _serializer.Deserialize(envelope.Payload, Type.GetType(envelope.MessageType)!);
        var context = new MessageContext(
            envelope.MessageId,
            args.RoutingKey,
            envelope.Timestamp,
            deliveryTag,
            channel,
            cancellationToken);

        if (_options.DeliveryControl.UseTransactions)
        {
            await channel.TxSelectAsync(cancellationToken);
        }

        try
        {
            List<Exception> exceptions = await InvokeAllHandlersAsync(handlers, message, context, cancellationToken);

            if (exceptions.Any())
            {
                throw new AggregateException("Ошибки в обработчиках", exceptions);
            }

            await CommitSuccessAsync(channel, deliveryTag, messageType, startTime, cancellationToken);
        }
        catch (Exception ex)
        {
            await HandleProcessingErrorAsync(
                envelope,
                messageType,
                channel,
                args,
                startTime,
                ex,
                cancellationToken);
        }
    }

    private async Task<List<Exception>> InvokeAllHandlersAsync(
        IReadOnlyList<ConsumerRegistration> handlers,
        object message,
        IMessageContext context,
        CancellationToken cancellationToken)
    {
        var exceptions = new List<Exception>();

        foreach (ConsumerRegistration handler in handlers)
        {
            try
            {
                await InvokeHandlerAsync(handler, message, context, cancellationToken);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
                _logger.LogError(ex, "Ошибка в обработчике {HandlerType}", handler.HandlerType.Name);
            }
        }

        return exceptions;
    }

    private async Task CommitSuccessAsync(
        IChannel channel,
        ulong deliveryTag,
        string messageType,
        DateTime startTime,
        CancellationToken cancellationToken)
    {
        if (_options.DeliveryControl.UseTransactions)
        {
            await channel.TxCommitAsync(cancellationToken);
            _metrics.TransactionCommitted(messageType);
        }
        else
        {
            await channel.BasicAckAsync(deliveryTag, false, cancellationToken);
        }

        TimeSpan duration = DateTime.UtcNow - startTime;
        _metrics.MessageProcessed(messageType, duration, true);
        _logger.LogDebug("Сообщение успешно обработано");
    }

    private async Task HandleProcessingErrorAsync(
        MessageEnvelope envelope,
        string messageType,
        IChannel channel,
        BasicDeliverEventArgs args,
        DateTime startTime,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (_deduplicationEnabled)
        {
            await deduplicationStore!.RemoveAsync(envelope.MessageId, cancellationToken);
            _logger.LogDebug("Удалена метка дубликата для {MessageId} после ошибки", envelope.MessageId);
        }

        if (_options.DeliveryControl.UseTransactions)
        {
            await channel.TxRollbackAsync(cancellationToken);
            _metrics.TransactionRolledBack(messageType);
        }

        // ✅ Вызов существующего метода
        await HandleProcessingFailureAsync(channel, args, envelope, exception, cancellationToken);

        TimeSpan duration = DateTime.UtcNow - startTime;
        _metrics.MessageProcessed(messageType, duration, false);
    }

    private async Task HandleCriticalErrorAsync(
        IChannel channel,
        ulong deliveryTag,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Критическая ошибка при обработке сообщения");

        try
        {
            // Проверяем, что канал еще открыт и сообщение не было подтверждено
            if (channel.IsOpen)
            {
                await channel.BasicNackAsync(deliveryTag, false, false, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Канал закрыт, невозможно отправить NACK для сообщения {DeliveryTag}", deliveryTag);
            }
        }
        catch (Exception nackEx)
        {
            _logger.LogError(nackEx, "Ошибка при отправке NACK для сообщения {DeliveryTag}", deliveryTag);
        }
    }

    private async Task InvokeHandlerAsync(
        ConsumerRegistration registration,
        object message,
        IMessageContext context,
        CancellationToken cancellationToken)
    {
        object? handler = CreateHandler(registration.HandlerType);

        if (handler == null)
        {
            throw new InvalidOperationException($"Не удалось создать обработчик {registration.HandlerType.Name}");
        }

        // Используем reflection для вызова метода HandleAsync
        MethodInfo? method = registration.HandlerType.GetMethod("HandleAsync");

        if (method == null)
        {
            throw new InvalidOperationException($"Тип {registration.HandlerType.Name} не содержит метод HandleAsync");
        }

        object[] parameters = new[] { message, context, cancellationToken };
        var task = method.Invoke(handler, parameters) as Task;

        if (task != null)
        {
            await task;
        }
    }

    private object? CreateHandler(Type handlerType)
    {
        if (serviceProvider != null)
        {
            try
            {
                return ActivatorUtilities.CreateInstance(serviceProvider, handlerType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании обработчика через DI");
                return null;
            }
        }

        // Для консольных приложений - конструктор без параметров
        try
        {
            return Activator.CreateInstance(handlerType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании обработчика через Activator");
            return null;
        }
    }

    private async Task HandleProcessingFailureAsync(
        IChannel channel,
        BasicDeliverEventArgs args,
        MessageEnvelope envelope,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ulong deliveryTag = args.DeliveryTag;
        string? messageType = envelope.MessageType;
        int retryAttempt = envelope.RetryAttempt;

        Exception actualException = exception is AggregateException agg
                                        ? agg.InnerException ?? exception
                                        : exception;

        // БИЗНЕС-ОШИБКИ - сразу в DLQ, без увеличения счетчика
        if (actualException is InvalidOperationException
            || actualException is ArgumentException
            || actualException is NotImplementedException)
        {
            _logger.LogWarning(
                exception,
                "Бизнес-ошибка, отправка сообщения {MessageId} в Dead Letter Queue",
                envelope.MessageId);

            await MoveToDeadLetterAsync(channel, envelope, args, exception.GetType().Name, cancellationToken);
            _metrics.MessageDeadLettered(messageType ?? "unknown", exception.GetType().Name);
            return;
        }

        // ПРОВЕРКА ПРЕВЫШЕНИЯ ПОПЫТОК
        if (retryAttempt >= _options.DeliveryControl.MaxRetryAttempts)
        {
            _logger.LogWarning(
                exception,
                "Отправка сообщения {MessageId} в Dead Letter Queue после {RetryAttempt} попыток",
                envelope.MessageId,
                retryAttempt);

            await MoveToDeadLetterAsync(channel, envelope, args, "max-retries-exceeded", cancellationToken);
            _metrics.MessageDeadLettered(messageType ?? "unknown", "max-retries-exceeded");
            return;
        }

        // УВЕЛИЧИВАЕМ СЧЕТЧИК ПОПЫТОК
        envelope.RetryAttempt++;
        _metrics.MessageRetried(messageType ?? "unknown", envelope.RetryAttempt);

        // RETRY ЛОГИКА
        if (_options.DeliveryControl.UseRequeueForRetries)
        {
            _logger.LogDebug(
                "Возврат сообщения {MessageId} в очередь (requeue), попытка {RetryAttempt}",
                envelope.MessageId,
                envelope.RetryAttempt);

            await channel.BasicNackAsync(deliveryTag, false, true, cancellationToken);
        }
        else
        {
            _logger.LogDebug(
                "Повторная публикация сообщения {MessageId} через {Delay}мс, попытка {RetryAttempt}",
                envelope.MessageId,
                CalculateRetryDelay(envelope.RetryAttempt).TotalMilliseconds,
                envelope.RetryAttempt);

            await channel.BasicNackAsync(deliveryTag, false, false, cancellationToken);

            TimeSpan delay = CalculateRetryDelay(envelope.RetryAttempt);
            await Task.Delay(delay, cancellationToken);

            byte[] body = _serializer.Serialize(envelope);
            BasicProperties props = CreateRetryProperties(args, envelope);

            await channel.BasicPublishAsync(
                args.Exchange ?? "",
                args.RoutingKey,
                false,
                props,
                body,
                cancellationToken);

            _logger.LogDebug(
                "Сообщение {MessageId} повторно опубликовано через тот же канал, попытка {RetryAttempt}",
                envelope.MessageId,
                envelope.RetryAttempt);
        }
    }

    private BasicProperties CreateRetryProperties(BasicDeliverEventArgs args, MessageEnvelope envelope)
    {
        var props = new BasicProperties
        {
            MessageId = envelope.MessageId,
            Type = envelope.MessageType,
            Headers = args.BasicProperties.Headers != null
                          ? new Dictionary<string, object?>(args.BasicProperties.Headers)
                          : new Dictionary<string, object?>(),
            ContentType = args.BasicProperties.ContentType ?? "application/json",
            ContentEncoding = args.BasicProperties.ContentEncoding ?? "utf-8",
            DeliveryMode = args.BasicProperties?.DeliveryMode ?? DeliveryModes.Persistent,
            Priority = args.BasicProperties?.Priority ?? 0,
            CorrelationId = args.BasicProperties?.CorrelationId,
            ReplyTo = args.BasicProperties?.ReplyTo,
            Expiration = args.BasicProperties?.Expiration,
            Timestamp = args.BasicProperties?.Timestamp ?? new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        props.Headers["x-retry-attempt"] = envelope.RetryAttempt;
        props.Headers["x-original-delivery-tag"] = args.DeliveryTag.ToString();

        return props;
    }

    private TimeSpan CalculateRetryDelay(int retryAttempt)
    {
        double delayMs = _options.DeliveryControl.RetryBaseDelayMs *
                         Math.Pow(_options.DeliveryControl.RetryDelayMultiplier, retryAttempt - 1);

        delayMs = Math.Min(delayMs, _options.DeliveryControl.RetryMaxDelayMs);

        return TimeSpan.FromMilliseconds(delayMs);
    }

    private async Task MoveToDeadLetterAsync(
        IChannel channel,
        MessageEnvelope envelope,
        BasicDeliverEventArgs args,
        string reason,
        CancellationToken cancellationToken)
    {
        if (!_options.DeliveryControl.EnableDeadLetter)
        {
            _logger.LogWarning("DLQ отключена, сообщение {MessageId} будет потеряно", envelope.MessageId);
            await channel.BasicAckAsync(args.DeliveryTag, false, cancellationToken);
            return;
        }

        envelope.Headers ??= new Dictionary<string, object>();
        envelope.Headers["x-death-reason"] = reason;
        envelope.Headers["x-death-time"] = DateTime.UtcNow.ToString("O");

        byte[] body = _serializer.Serialize(envelope);

        var props = new BasicProperties
        {
            MessageId = envelope.MessageId,
            Type = envelope.MessageType,
            Headers = envelope.Headers?.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value)
        };

        await channel.BasicPublishAsync(
            _options.DeliveryControl.DeadLetterExchange,
            _options.DeliveryControl.DeadLetterRoutingKey,
            false,
            props,
            body,
            cancellationToken);

        await channel.BasicAckAsync(args.DeliveryTag, false, cancellationToken);

        _logger.LogWarning(
            "Сообщение {MessageId} отправлено в DLQ ({Exchange}/{RoutingKey}), причина: {Reason}",
            envelope.MessageId,
            _options.DeliveryControl.DeadLetterExchange,
            _options.DeliveryControl.DeadLetterRoutingKey,
            reason);
    }

    #endregion

}
