namespace RabbitMQ.Module.Contracts;

/// <summary>
/// Контекст сообщения, доступный в обработчике
/// </summary>
public interface IMessageContext
{

    #region Properties

    /// <summary>
    /// ID сообщения (для дедубликации)
    /// </summary>
    string MessageId { get; }

    /// <summary>
    /// Routing key, с которым пришло сообщение
    /// </summary>
    string RoutingKey { get; }

    /// <summary>
    /// Временная метка сообщения
    /// </summary>
    DateTime Timestamp { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Подтвердить обработку сообщения
    /// </summary>
    Task AckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Отклонить сообщение (с возможностью рекью)
    /// </summary>
    Task NackAsync(bool requeue = false, CancellationToken cancellationToken = default);

    #endregion

}
