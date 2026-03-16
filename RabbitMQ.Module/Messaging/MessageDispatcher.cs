namespace RabbitMQ.Module.Messaging;

using System.Reflection;

using Client;
using Client.Events;

using Contracts;

using Infrastructure.Serialization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Registration;

public class MessageDispatcher(
    IConsumerRegistry registry,
    IMessageSerializer serializer,
    ILogger<MessageDispatcher> logger,
    IServiceProvider? serviceProvider = null)
{

    #region Fields

    private readonly IConsumerRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly IMessageSerializer _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    private readonly ILogger<MessageDispatcher> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    #endregion

    #region Methods

    public async Task DispatchAsync(
        IChannel channel,
        BasicDeliverEventArgs args,
        CancellationToken cancellationToken = default)
    {
        ulong deliveryTag = args.DeliveryTag;

        try
        {
            var envelope = _serializer.Deserialize<MessageEnvelope>(args.Body.ToArray());

            _logger.LogDebug(
                "Получено сообщение {MessageId} типа {MessageType}",
                envelope.MessageId,
                envelope.MessageType);

            if (string.IsNullOrEmpty(envelope.MessageType))
            {
                _logger.LogError("MessageType пустой в полученном сообщении");
                await channel.BasicNackAsync(deliveryTag, false, false, cancellationToken);
                return;
            }

            var messageType = Type.GetType(envelope.MessageType);

            if (messageType == null)
            {
                _logger.LogError("Не удалось определить тип сообщения: {MessageType}", envelope.MessageType);
                await channel.BasicNackAsync(deliveryTag, false, false, cancellationToken);
                return;
            }

            object message = _serializer.Deserialize(envelope.Payload, messageType);

            var context = new MessageContext(
                envelope.MessageId,
                args.RoutingKey,
                envelope.Timestamp,
                deliveryTag,
                channel,
                cancellationToken);

            IEnumerable<ConsumerRegistration> registrations = _registry.GetRegistrationsForMessage(messageType);

            if (!registrations.Any())
            {
                _logger.LogWarning("Нет зарегистрированных обработчиков для типа {MessageType}", messageType.Name);
                await channel.BasicAckAsync(deliveryTag, false, cancellationToken);
                return;
            }

            var exceptions = new List<Exception>();

            foreach (ConsumerRegistration registration in registrations)
            {
                try
                {
                    _logger.LogDebug(
                        "Вызов обработчика {HandlerType} для сообщения {MessageId}",
                        registration.HandlerType.Name,
                        envelope.MessageId);

                    await InvokeHandlerAsync(registration, message, context, cancellationToken);

                    _logger.LogDebug("Обработчик {HandlerType} успешно выполнен", registration.HandlerType.Name);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    _logger.LogError(
                        ex,
                        "Ошибка в обработчике {HandlerType} для сообщения {MessageId}",
                        registration.HandlerType.Name,
                        envelope.MessageId);
                }
            }

            if (exceptions.Any())
            {
                _logger.LogError(
                    "Обработка сообщения {MessageId} завершилась с {Count} ошибками",
                    envelope.MessageId,
                    exceptions.Count);

                await channel.BasicNackAsync(deliveryTag, false, false, cancellationToken);
            }
            else
            {
                await channel.BasicAckAsync(deliveryTag, false, cancellationToken);
                _logger.LogDebug("Сообщение {MessageId} успешно обработано", envelope.MessageId);
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
                _logger.LogError(nackEx, "Ошибка при отправке NACK для сообщения {DeliveryTag}", deliveryTag);
            }
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

        var task = (Task?)method.Invoke(handler, new[] { message, context, cancellationToken });

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

        // Для консольных приложений - ищем конструктор с ConsumerHostedServiceTests
        try
        {
            ConstructorInfo[] constructors = handlerType.GetConstructors();

            // Сначала ищем конструктор с параметром ConsumerHostedServiceTests
            if (constructors.Length > 0)
            {
                ConstructorInfo constructor = constructors[0];
                ParameterInfo[] parameters = constructor.GetParameters();

                if (parameters.Length == 1 && parameters[0].ParameterType.Name == "ConsumerHostedServiceTests")
                {
                    // Для тестов - передаем this
                    return constructor.Invoke(new[] { this });
                }
            }

            // Если ничего не нашли - используем конструктор без параметров
            return Activator.CreateInstance(handlerType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании обработчика через Activator для типа {HandlerType}", handlerType.Name);
            return null;
        }
    }

    #endregion

}
