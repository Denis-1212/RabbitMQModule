namespace RabbitMQ.Module.Contracts;

/// <summary>
/// Интерфейс для обработчика сообщений
/// </summary>
/// <typeparam name = "T">Тип обрабатываемого сообщения</typeparam>
public interface IMessageHandler<T>
{

    #region Methods

    /// <summary>
    /// Обрабатывает полученное сообщение
    /// </summary>
    /// <param name = "message">Сообщение</param>
    /// <param name = "context">Контекст сообщения</param>
    /// <param name = "cancellationToken">Токен отмены</param>
    Task HandleAsync(T message, IMessageContext context, CancellationToken cancellationToken);

    #endregion

}
