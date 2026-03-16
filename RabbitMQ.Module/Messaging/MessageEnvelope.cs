namespace RabbitMQ.Module.Messaging;

using Infrastructure.Serialization;

/// <summary>
/// Конверт сообщения для передачи через RabbitMQ
/// </summary>
public class MessageEnvelope
{

    #region Properties

    /// <summary>
    /// Тип сообщения (AssemblyQualifiedName)
    /// </summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// Уникальный идентификатор сообщения
    /// </summary>
    public string MessageId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Payload сообщения (сериализованный объект)
    /// </summary>
    public byte[] Payload { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Временная метка создания
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Метаданные сообщения
    /// </summary>
    public Dictionary<string, object>? Headers { get; set; }

    #endregion

    #region Methods

    public static MessageEnvelope Create<T>(T message, IMessageSerializer serializer, string? messageId = null)
    {
        var envelope = new MessageEnvelope
        {
            MessageType = typeof(T).AssemblyQualifiedName
                          ?? throw new InvalidOperationException("Не удалось получить AssemblyQualifiedName для типа " + typeof(T).FullName),
            MessageId = messageId ?? Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow
        };

        envelope.Payload = serializer.Serialize(message);

        return envelope;
    }

    #endregion

}
