namespace RabbitMQ.Module.DeliveryControl;

/// <summary>
/// Исключение, возникающее при сбое доставки сообщения
/// </summary>
public class DeliveryFailureException : Exception
{

    #region Properties

    /// <summary>
    /// ID сообщения, которое не удалось доставить
    /// </summary>
    public string? MessageId { get; set; }

    /// <summary>
    /// Причина отказа от брокера
    /// </summary>
    public string? Reason { get; set; }

    #endregion

    #region Constructors

    public DeliveryFailureException(string message)
        : base(message)
    {
    }

    public DeliveryFailureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    #endregion

}
