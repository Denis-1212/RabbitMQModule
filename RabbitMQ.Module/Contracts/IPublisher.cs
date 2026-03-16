namespace RabbitMQ.Module.Contracts;

using Configuration;

/// <summary>
/// Интерфейс для публикации сообщений в RabbitMQ
/// </summary>
public interface IPublisher
{

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

    #endregion

}
