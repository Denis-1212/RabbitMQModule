namespace RabbitMQ.TestApp.Web.Models;

/// <summary>
/// Запрос на отправку сообщения
/// </summary>
public class SendMessageRequest
{

    #region Properties

    public string Text { get; set; } = string.Empty;
    public string? Sender { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }

    #endregion

}
