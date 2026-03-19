namespace RabbitMQ.Module.Messaging;

using System.Reflection;

using Client;
using Client.Events;

using Configuration;

using Contracts;

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
    IServiceProvider? serviceProvider = null
)
{

    #region Fields

    private readonly IConsumerRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly IMessageSerializer _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    private readonly ILogger<MessageDispatcher> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly MessagingOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly IDeliveryMetrics _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));

    #endregion

    #region Methods

    public async Task DispatchAsync(
        IChannel channel,
        BasicDeliverEventArgs args,
        CancellationToken cancellationToken = default)
    {
        ulong deliveryTag = args.DeliveryTag;
        DateTime startTime = DateTime.UtcNow;
        MessageEnvelope? envelope = null;
        string? messageType = null;

        // _logger.LogTrace(
        //     "📥📥📥 RAW MESSAGE RECEIVED: {Body}",
        //     Encoding.UTF8.GetString(args.Body.ToArray()));

        try
        {
            envelope = _serializer.Deserialize<MessageEnvelope>(args.Body.ToArray());
            messageType = envelope.MessageType;

            _logger.LogDebug(
                "Получено сообщение {MessageId} типа {MessageType}, попытка {RetryAttempt}",
                envelope.MessageId,
                envelope.MessageType,
                envelope.RetryAttempt);

            if (string.IsNullOrEmpty(envelope.MessageType))
            {
                _logger.LogError("MessageType пустой в полученном сообщении");
                await channel.BasicNackAsync(deliveryTag, false, false, cancellationToken);
                return;
            }

            var messageTypeObj = Type.GetType(envelope.MessageType);

            if (messageTypeObj == null)
            {
                _logger.LogError("Не удалось определить тип сообщения: {MessageType}", envelope.MessageType);
                await MoveToDeadLetterAsync(channel, envelope, args, "unknown-type", cancellationToken);
                return;
            }

            object message = _serializer.Deserialize(envelope.Payload, messageTypeObj);

            var context = new MessageContext(
                envelope.MessageId,
                args.RoutingKey,
                envelope.Timestamp,
                deliveryTag,
                channel,
                cancellationToken);

            IEnumerable<ConsumerRegistration> registrations = _registry.GetRegistrationsForMessage(messageTypeObj);

            if (!registrations.Any())
            {
                _logger.LogWarning("Нет зарегистрированных обработчиков для типа {MessageType}", messageTypeObj.Name);
                await channel.BasicAckAsync(deliveryTag, false, cancellationToken);
                return;
            }

            // Обработка с поддержкой транзакций
            if (_options.DeliveryControl.UseTransactions)
            {
                await channel.TxSelectAsync(cancellationToken);
            }

            try
            {
                var exceptions = new List<Exception>();

                foreach (ConsumerRegistration registration in registrations)
                {
                    try
                    {
                        await InvokeHandlerAsync(registration, message, context, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }

                if (exceptions.Any())
                {
                    throw new AggregateException("Ошибки в обработчиках", exceptions);
                }

                // Успех
                if (_options.DeliveryControl.UseTransactions)
                {
                    await channel.TxCommitAsync(cancellationToken);
                    _metrics.TransactionCommitted(messageType ?? "unknown");
                }
                else
                {
                    await channel.BasicAckAsync(deliveryTag, false, cancellationToken);
                }

                TimeSpan duration = DateTime.UtcNow - startTime;
                _metrics.MessageProcessed(messageType ?? "unknown", duration, true);
                _logger.LogDebug("Сообщение {MessageId} успешно обработано", envelope.MessageId);
            }
            catch (Exception ex)
            {
                if (_options.DeliveryControl.UseTransactions)
                {
                    await channel.TxRollbackAsync(cancellationToken);
                    _metrics.TransactionRolledBack(messageType ?? "unknown");
                }

                await HandleProcessingFailureAsync(channel, args, envelope, ex, cancellationToken);

                TimeSpan duration = DateTime.UtcNow - startTime;
                _metrics.MessageProcessed(messageType ?? "unknown", duration, false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка при обработке сообщения");

            try
            {
                await channel.BasicNackAsync(deliveryTag, false, false, cancellationToken);
            }
            catch (Exception nackEx)
            {
                _logger.LogError(nackEx, "Ошибка при отправке NACK");
            }
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

        // ✅ 1. БИЗНЕС-ОШИБКИ - сразу в DLQ, без увеличения счетчика
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

        // ✅ 2. ПРОВЕРКА ПРЕВЫШЕНИЯ ПОПЫТОК
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

        // ✅ 3. УВЕЛИЧИВАЕМ СЧЕТЧИК ПОПЫТОК
        envelope.RetryAttempt++;
        _metrics.MessageRetried(messageType ?? "unknown", envelope.RetryAttempt);

        // ✅ 4. RETRY ЛОГИКА
        if (_options.DeliveryControl.UseRequeueForRetries)
        {
            // Простой nack с requeue - сообщение вернется в очередь немедленно
            _logger.LogDebug(
                "Возврат сообщения {MessageId} в очередь (requeue), попытка {RetryAttempt}",
                envelope.MessageId,
                envelope.RetryAttempt);

            await channel.BasicNackAsync(deliveryTag, false, true, cancellationToken);
        }
        else
        {
            // Отклоняем без requeue и публикуем заново с задержкой
            _logger.LogDebug(
                "Повторная публикация сообщения {MessageId} через {Delay}мс, попытка {RetryAttempt}",
                envelope.MessageId,
                CalculateRetryDelay(envelope.RetryAttempt).TotalMilliseconds,
                envelope.RetryAttempt);

            await channel.BasicNackAsync(deliveryTag, false, false, cancellationToken);

            // Ждем перед повторной публикацией
            TimeSpan delay = CalculateRetryDelay(envelope.RetryAttempt);
            await Task.Delay(delay, cancellationToken);

            // Публикуем через тот же канал
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
            Headers = args.BasicProperties?.Headers != null
                          ? new Dictionary<string, object?>(args.BasicProperties.Headers)
                          : new Dictionary<string, object?>(),
            ContentType = args.BasicProperties?.ContentType ?? "application/json",
            ContentEncoding = args.BasicProperties?.ContentEncoding ?? "utf-8",
            DeliveryMode = args.BasicProperties?.DeliveryMode ?? DeliveryModes.Persistent,
            Priority = args.BasicProperties?.Priority ?? 0,
            CorrelationId = args.BasicProperties?.CorrelationId,
            ReplyTo = args.BasicProperties?.ReplyTo,
            Expiration = args.BasicProperties?.Expiration,
            Timestamp = args.BasicProperties?.Timestamp ?? new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        // Добавляем информацию о повторной попытке
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

        // Добавляем информацию о причине смерти
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

        // ✅ ПУБЛИКУЕМ В DLX, А НЕ В ИСХОДНУЮ ОЧЕРЕДЬ
        await channel.BasicPublishAsync(
            _options.DeliveryControl.DeadLetterExchange,
            _options.DeliveryControl.DeadLetterRoutingKey,
            false,
            props,
            body,
            cancellationToken);

        // Подтверждаем оригинальное сообщение (удаляем из исходной очереди)
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
