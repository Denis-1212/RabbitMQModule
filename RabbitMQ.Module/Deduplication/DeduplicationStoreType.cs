namespace RabbitMQ.Module.Deduplication;

/// <summary>
/// Тип хранилища дедубликации
/// </summary>
public enum DeduplicationStoreType
{
    /// <summary>
    /// Дедубликация отключена
    /// </summary>
    None,

    /// <summary>
    /// Встроенное in-memory хранилище
    /// </summary>
    InMemory,

    /// <summary>
    /// Внешнее Redis хранилище
    /// </summary>
    Redis
}
