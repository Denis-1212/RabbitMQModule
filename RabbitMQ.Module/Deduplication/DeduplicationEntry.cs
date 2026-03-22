namespace RabbitMQ.Module.Deduplication;

/// <summary>
/// Внутренняя запись для хранилища дедубликации
/// </summary>
internal class DeduplicationEntry
{

    #region Properties

    public string MessageId { get; init; } = string.Empty;
    public DateTime ProcessedAt { get; init; }
    public TimeSpan Ttl { get; init; }

    #endregion

}
