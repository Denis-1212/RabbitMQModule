namespace RabbitMQ.Module.DeliveryControl;

using Contracts;

using Microsoft.Extensions.Logging;

/// <summary>
/// Реализация метрик по умолчанию (логирование)
/// </summary>
public class DefaultDeliveryMetrics : IDeliveryMetrics
{

    #region Fields

    private readonly ILogger<DefaultDeliveryMetrics> _logger;

    #endregion

    #region Constructors

    public DefaultDeliveryMetrics(ILogger<DefaultDeliveryMetrics> logger)
    {
        _logger = logger;
    }

    #endregion

    #region Methods

    public void MessageProcessed(string messageType, TimeSpan duration, bool success)
    {
        _logger.LogInformation(
            "Metric|Processed|{Type}|{DurationMs}|{Success}",
            messageType,
            duration.TotalMilliseconds,
            success);
    }

    public void MessageRetried(string messageType, int attempt)
    {
        _logger.LogInformation("Metric|Retry|{Type}|{Attempt}", messageType, attempt);
    }

    public void MessageDeadLettered(string messageType, string reason)
    {
        _logger.LogWarning("Metric|DeadLetter|{Type}|{Reason}", messageType, reason);
    }

    public void TransactionCommitted(string messageType)
    {
        _logger.LogDebug("Metric|TxCommit|{Type}", messageType);
    }

    public void TransactionRolledBack(string messageType)
    {
        _logger.LogWarning("Metric|TxRollback|{Type}", messageType);
    }

    #endregion

}
