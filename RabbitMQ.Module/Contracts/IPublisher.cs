namespace RabbitMQ.Module.Contracts;

using Configuration;

using Messaging;

/// <summary>
/// Интерфейс для публикации сообщений в RabbitMQ
/// </summary>
public interface IPublisher
{

    #region Properties

    /// <summary>
    /// Было ли последнее сообщение подтверждено брокером
    /// </summary>
    bool LastPublishWasConfirmed { get; }

    /// <summary>
    /// Задержка между публикацией и подтверждением последнего сообщения
    /// </summary>
    TimeSpan? LastConfirmLatency { get; }

    /// <summary>
    /// ID последнего опубликованного сообщения
    /// </summary>
    string? LastMessageId { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Публикует сообщение в RabbitMQ
    /// </summary>
    /// <typeparam name = "T">Тип сообщения</typeparam>
    /// <param name = "message">Сообщение для публикации</param>
    /// <param name = "configure">Дополнительная конфигурация публикации</param>
    /// <param name = "cancellationToken">Токен отмены</param>
    Task PublishAsync<T>(
        T message,
        Action<IPublishConfiguration>? configure = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Публикует готовый конверт сообщения (для повторной публикации)
    /// </summary>
    /// <param name = "envelope">Конверт сообщения</param>
    /// <param name = "configure">Дополнительная конфигурация публикации</param>
    /// <param name = "cancellationToken">Токен отмены</param>
    Task PublishAsync(
        MessageEnvelope envelope,
        Action<IPublishConfiguration>? configure = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Сбрасывает статистику публикации (для тестов)
    /// </summary>
    void ResetStats();

    #endregion

}
