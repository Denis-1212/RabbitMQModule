namespace RabbitMQ.Module.Configuration;

using System.Text;

using Client;

/// <summary>
/// Конфигурация публикации сообщения
/// </summary>
public class PublishConfiguration : IPublishConfiguration
{

    #region Fields

    private TimeSpan? _expiration;
    private readonly Dictionary<string, object> _headers = new();
    private readonly Dictionary<string, object> _customProperties = new();

    #endregion

    #region Properties

    /// <summary>
    /// Exchange для публикации (по умолчанию пусто - используется default exchange)
    /// </summary>
    public string Exchange { get; private set; } = string.Empty;

    /// <summary>
    /// Routing key для публикации
    /// </summary>
    public string RoutingKey { get; private set; } = string.Empty;

    /// <summary>
    /// ID сообщения (для дедубликации)
    /// </summary>
    public string? MessageId { get; private set; }

    /// <summary>
    /// Флаг mandatory - если true и сообщение не может быть доставлено, оно возвращается отправителю
    /// </summary>
    public bool Mandatory { get; private set; }

    /// <summary>
    /// Флаг ожидания подтверждения от RabbitMQ (Publisher Confirms)
    /// </summary>
    public bool UsePublisherConfirms { get; private set; } = true;

    /// <summary>
    /// Время жизни сообщения (TTL)
    /// </summary>
    public TimeSpan? Expiration => _expiration;

    /// <summary>
    /// Приоритет сообщения (0-9)
    /// </summary>
    public byte Priority { get; private set; }

    /// <summary>
    /// ID корреляции (для RPC паттернов)
    /// </summary>
    public string? CorrelationId { get; private set; }

    /// <summary>
    /// Очередь для ответов (для RPC паттернов)
    /// </summary>
    public string? ReplyTo { get; private set; }

    /// <summary>
    /// Заголовки сообщения
    /// </summary>
    public IReadOnlyDictionary<string, object> Headers => _headers;

    /// <summary>
    /// Пользовательские свойства сообщения
    /// </summary>
    public IReadOnlyDictionary<string, object> CustomProperties => _customProperties;

    #endregion

    #region Methods

    /// <inheritdoc/>
    public void WithExchange(string exchange)
    {
        ArgumentNullException.ThrowIfNull(exchange);
        Exchange = exchange;
    }

    /// <inheritdoc/>
    public void WithRoutingKey(string routingKey)
    {
        ArgumentNullException.ThrowIfNull(routingKey);
        RoutingKey = routingKey;
    }

    /// <inheritdoc/>
    public void WithMessageId(string messageId)
    {
        ArgumentNullException.ThrowIfNull(messageId);
        MessageId = messageId;
    }

    /// <inheritdoc/>
    public void WithMandatory(bool mandatory = true)
    {
        Mandatory = mandatory;
    }

    /// <inheritdoc/>
    public void WithPublisherConfirms(bool waitForConfirms = true) // Переименован метод
    {
        UsePublisherConfirms = waitForConfirms;
    }

    /// <inheritdoc/>
    public void WithHeader(string key, object value)
    {
        ArgumentNullException.ThrowIfNull(key);
        _headers[key] = value;
    }

    /// <inheritdoc/>
    public void WithHeaders(IDictionary<string, object> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        foreach ((string key, object value) in headers)
        {
            _headers[key] = value;
        }
    }

    /// <inheritdoc/>
    public void WithExpiration(TimeSpan expiration)
    {
        if (expiration <= TimeSpan.Zero)
        {
            throw new ArgumentException("Expiration must be positive", nameof(expiration));
        }

        _expiration = expiration;
    }

    /// <inheritdoc/>
    public void WithPriority(byte priority)
    {
        if (priority > 9)
        {
            throw new ArgumentException("Priority must be between 0 and 9", nameof(priority));
        }

        Priority = priority;
    }

    /// <inheritdoc/>
    public void WithCorrelationId(string correlationId)
    {
        ArgumentNullException.ThrowIfNull(correlationId);
        CorrelationId = correlationId;
    }

    /// <inheritdoc/>
    public void WithReplyTo(string replyTo)
    {
        ArgumentNullException.ThrowIfNull(replyTo);
        ReplyTo = replyTo;
    }

    /// <inheritdoc/>
    public void WithCustomProperty(string key, object value)
    {
        ArgumentNullException.ThrowIfNull(key);
        _customProperties[key] = value;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append($"Exchange: '{Exchange}', RoutingKey: '{RoutingKey}'");

        if (!string.IsNullOrEmpty(MessageId))
        {
            sb.Append($", MessageId: {MessageId}");
        }

        if (Mandatory)
        {
            sb.Append(", Mandatory: true");
        }

        if (!UsePublisherConfirms)
        {
            sb.Append(", UsePublisherConfirms: false");
        }

        if (_expiration.HasValue)
        {
            sb.Append($", Expiration: {_expiration.Value.TotalMilliseconds}ms");
        }

        if (Priority > 0)
        {
            sb.Append($", Priority: {Priority}");
        }

        if (!string.IsNullOrEmpty(CorrelationId))
        {
            sb.Append($", CorrelationId: {CorrelationId}");
        }

        if (!string.IsNullOrEmpty(ReplyTo))
        {
            sb.Append($", ReplyTo: {ReplyTo}");
        }

        if (_headers.Any())
        {
            sb.Append($", Headers: {_headers.Count}");
        }

        if (_customProperties.Any())
        {
            sb.Append($", CustomProps: {_customProperties.Count}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Создает свойства сообщения для RabbitMQ
    /// </summary>
    internal BasicProperties CreateBasicProperties()
    {
        var props = new BasicProperties
        {
            MessageId = MessageId ?? Guid.NewGuid().ToString(),
            Type = null,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            DeliveryMode = DeliveryModes.Persistent,
            Priority = Priority
        };

        if (!string.IsNullOrEmpty(CorrelationId))
        {
            props.CorrelationId = CorrelationId;
        }

        if (!string.IsNullOrEmpty(ReplyTo))
        {
            props.ReplyTo = ReplyTo;
        }

        if (_expiration.HasValue)
        {
            props.Expiration = _expiration.Value.TotalMilliseconds.ToString();
        }

        // Конвертируем Dictionary<string, object> в Dictionary<string, object?>
        var headers = new Dictionary<string, object?>();

        foreach (KeyValuePair<string, object> kvp in _headers)
        {
            headers[kvp.Key] = kvp.Value;
        }

        // Добавляем пользовательские свойства в заголовки
        foreach ((string key, object value) in _customProperties)
        {
            headers[$"x-{key}"] = value;
        }

        if (headers.Any())
        {
            props.Headers = headers;
        }

        return props;
    }

    /// <summary>
    /// Сбрасывает конфигурацию к значениям по умолчанию
    /// </summary>
    internal void Reset()
    {
        Exchange = string.Empty;
        RoutingKey = string.Empty;
        MessageId = null;
        Mandatory = false;
        UsePublisherConfirms = true;
        _expiration = null;
        Priority = 0;
        CorrelationId = null;
        ReplyTo = null;
        _headers.Clear();
        _customProperties.Clear();
    }

    #endregion

}
