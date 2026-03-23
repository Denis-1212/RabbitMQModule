namespace RabbitMQ.TestApp.Web.Models;

/// <summary>
/// Обработанное сообщение (для истории)
/// </summary>
public class ProcessedMessage
{

    #region Properties

    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? Sender { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime ProcessedAt { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }

    #endregion

}
