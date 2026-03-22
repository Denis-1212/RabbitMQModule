namespace RabbitMQ.Module.Contracts;

/// <summary>
/// Интерфейс для сбора метрик доставки
/// </summary>
public interface IDeliveryMetrics
{

    #region Methods

    /// <summary>
    /// Сообщение обработано
    /// </summary>
    void MessageProcessed(string messageType, TimeSpan duration, bool success);

    /// <summary>
    /// Сообщение отправлено на повторную попытку
    /// </summary>
    void MessageRetried(string messageType, int attempt);

    /// <summary>
    /// Сообщение отправлено в Dead Letter Queue
    /// </summary>
    void MessageDeadLettered(string messageType, string reason);

    /// <summary>
    /// Транзакция закоммичена
    /// </summary>
    void TransactionCommitted(string messageType);

    /// <summary>
    /// Транзакция откачена
    /// </summary>
    void TransactionRolledBack(string messageType);

    /// <summary>
    /// Сообщение пропущено как дубликат
    /// </summary>
    void MessageDeduplicated(string messageType);

    /// <summary>
    /// Ошибка в хранилище дедубликации
    /// </summary>
    void DeduplicationStoreError(string messageType, string error);

    #endregion

}
