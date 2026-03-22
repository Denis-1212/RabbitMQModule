namespace RabbitMQ.TestApp.Web.Models;

/// <summary>
/// Универсальное сообщение для демонстрации
/// </summary>
public class GenericMessage
{

    #region Properties

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Text { get; set; } = string.Empty;
    public string? Sender { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    #endregion

}
