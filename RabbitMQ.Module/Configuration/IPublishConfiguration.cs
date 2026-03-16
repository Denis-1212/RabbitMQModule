namespace RabbitMQ.Module.Configuration;

/// <summary>
/// Интерфейс для конфигурации публикации сообщения
/// </summary>
public interface IPublishConfiguration
{

    #region Methods

    /// <summary>
    /// Устанавливает exchange для публикации
    /// </summary>
    /// <param name = "exchange">Имя exchange (пустая строка для default exchange)</param>
    void WithExchange(string exchange);

    /// <summary>
    /// Устанавливает routing key для публикации
    /// </summary>
    /// <param name = "routingKey">Routing key</param>
    void WithRoutingKey(string routingKey);

    /// <summary>
    /// Устанавливает ID сообщения (для дедубликации)
    /// </summary>
    /// <param name = "messageId">Уникальный идентификатор сообщения</param>
    void WithMessageId(string messageId);

    /// <summary>
    /// Включает mandatory режим - если true и сообщение не может быть доставлено,
    /// оно возвращается отправителю через Basic.Return
    /// </summary>
    /// <param name = "mandatory">Флаг mandatory</param>
    void WithMandatory(bool mandatory = true);

    /// <summary>
    /// Включает режим ожидания подтверждения от RabbitMQ (Publisher Confirms)
    /// </summary>
    /// <param name = "waitForConfirms">true для ожидания подтверждения</param>
    void WithPublisherConfirms(bool waitForConfirms = true);

    /// <summary>
    /// Добавляет заголовок к сообщению
    /// </summary>
    /// <param name = "key">Ключ заголовка</param>
    /// <param name = "value">Значение заголовка</param>
    void WithHeader(string key, object value);

    /// <summary>
    /// Добавляет несколько заголовков к сообщению
    /// </summary>
    /// <param name = "headers">Словарь заголовков</param>
    void WithHeaders(IDictionary<string, object> headers);

    /// <summary>
    /// Устанавливает время жизни сообщения (TTL)
    /// </summary>
    /// <param name = "expiration">Время жизни</param>
    void WithExpiration(TimeSpan expiration);

    /// <summary>
    /// Устанавливает приоритет сообщения (0-9)
    /// </summary>
    /// <param name = "priority">Приоритет</param>
    void WithPriority(byte priority);

    /// <summary>
    /// Устанавливает ID корреляции (для RPC паттернов)
    /// </summary>
    /// <param name = "correlationId">ID корреляции</param>
    void WithCorrelationId(string correlationId);

    /// <summary>
    /// Устанавливает очередь для ответов (для RPC паттернов)
    /// </summary>
    /// <param name = "replyTo">Имя очереди для ответов</param>
    void WithReplyTo(string replyTo);

    /// <summary>
    /// Добавляет пользовательское свойство (будет добавлено в заголовки с префиксом x-)
    /// </summary>
    /// <param name = "key">Ключ свойства</param>
    /// <param name = "value">Значение свойства</param>
    void WithCustomProperty(string key, object value);

    #endregion

}
