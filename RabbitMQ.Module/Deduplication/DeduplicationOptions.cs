namespace RabbitMQ.Module.Deduplication;

/// <summary>
/// Настройки дедубликации сообщений
/// </summary>
public class DeduplicationOptions
{

    #region Properties

    /// <summary>
    /// Тип хранилища дедубликации
    /// </summary>
    public DeduplicationStoreType StoreType { get; set; } = DeduplicationStoreType.InMemory;

    /// <summary>
    /// Время жизни ID в хранилище (по умолчанию 24 часа)
    /// </summary>
    public TimeSpan Ttl { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Префикс ключей (для Redis)
    /// </summary>
    public string KeyPrefix { get; set; } = "msg:";

    /// <summary>
    /// Строка подключения к Redis (для StoreType = Redis)
    /// </summary>
    public string? RedisConnectionString { get; set; }

    /// <summary>
    /// Включить fallback на In-Memory при недоступности Redis
    /// </summary>
    public bool EnableRedisFallback { get; set; } = false;

    /// <summary>
    /// Логировать ошибки подключения к Redis
    /// </summary>
    public bool LogRedisErrors { get; set; } = true;

    #endregion

    #region Methods

    internal void Validate()
    {
        if (StoreType == DeduplicationStoreType.Redis && string.IsNullOrWhiteSpace(RedisConnectionString))
        {
            throw new InvalidOperationException("RedisConnectionString обязателен при StoreType = Redis");
        }

        if (Ttl <= TimeSpan.Zero)
        {
            throw new InvalidOperationException($"Ttl должен быть больше 0: {Ttl}");
        }

        if (Ttl > TimeSpan.FromDays(7))
        {
            throw new InvalidOperationException($"Ttl не может превышать 7 дней: {Ttl}");
        }
    }

    #endregion

}
