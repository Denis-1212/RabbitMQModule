namespace RabbitMQ.Module.Deduplication;

/// <summary>
/// Интерфейс хранилища для дедубликации сообщений
/// </summary>
public interface IDeduplicationStore : IAsyncDisposable
{

    #region Methods

    /// <summary>
    /// Атомарно пытается добавить messageId в хранилище
    /// </summary>
    /// <returns>true — если добавлен (первое сообщение), false — если уже существует</returns>
    Task<bool> TryAddAsync(string messageId, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет messageId из хранилища (при ошибке обработки)
    /// </summary>
    Task RemoveAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет существование messageId (для мониторинга)
    /// </summary>
    Task<bool> ExistsAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает все активные ключи (для отладки)
    /// </summary>
    Task<IReadOnlyCollection<string>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Очищает хранилище (для тестов)
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);

    #endregion

}
