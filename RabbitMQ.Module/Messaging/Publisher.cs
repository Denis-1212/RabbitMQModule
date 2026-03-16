namespace RabbitMQ.Module.Messaging;

using Client;
using Client.Exceptions;

using Configuration;

using Contracts;

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

    #endregion

    #region Constructors

    public Publisher(
        IChannelPool channelPool,
        IMessageSerializer serializer,
        ILogger<Publisher> logger)
    {
        _channelPool = channelPool ?? throw new ArgumentNullException(nameof(channelPool));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _retryPolicy = Policy
            .Handle<AlreadyClosedException>()
            .Or<OperationInterruptedException>()
            .Or<BrokerUnreachableException>()
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

    /// <inheritdoc/>
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

                BasicProperties props = config.CreateBasicProperties();
                props.Type = envelope.MessageType; // Кладем тип в свойства RabbitMQ
                props.MessageId = envelope.MessageId;
                props.Headers ??= new Dictionary<string, object?>();
                props.Headers["x-message-type"] = envelope.MessageType; // Дублируем в заголовки

                _logger.LogDebug(
                    "Публикация сообщения {MessageId} типа {MessageType}",
                    envelope.MessageId,
                    envelope.MessageType);

                // Сериализуем весь envelope, а не только message
                byte[] body = _serializer.Serialize(envelope);

                await channel.BasicPublishAsync(
                    config.Exchange,
                    config.RoutingKey,
                    config.Mandatory,
                    props,
                    body,
                    cancellationToken);

                _logger.LogTrace("Сообщение {MessageId} опубликовано", envelope.MessageId);
            }
            finally
            {
                await _channelPool.ReturnAsync(channel);
            }
        });
    }

    #endregion

}
