// Добавить

namespace RabbitMQ.Module.Configuration;

using System.Threading.RateLimiting;

/// <summary>
/// Настройки контроля доставки сообщений
/// </summary>
public class DeliveryControlOptions
{

    #region Properties

    /// <summary>
    /// Таймаут ожидания подтверждения публикации (мс)
    /// </summary>
    public int PublishConfirmationTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Включить Publisher Confirms
    /// </summary>
    public bool PublisherConfirmsEnabled { get; set; } = true;

    /// <summary>
    /// Включить отслеживание подтверждений
    /// </summary>
    public bool PublisherConfirmationTrackingEnabled { get; set; } = true;

    /// <summary>
    /// Лимитер скорости для неподтвержденных сообщений
    /// Если null - используется встроенный ThrottlingRateLimiter от RabbitMQ
    /// </summary>
    public RateLimiter? OutstandingPublisherConfirmationsRateLimiter { get; set; }

    /// <summary>
    /// Максимальное количество неподтвержденных сообщений
    /// 0 = не ограничено (используется дефолтный лимитер RabbitMQ)
    /// </summary>
    public int MaxOutstandingPublisherConfirmations { get; set; } = 128; // Дефолт RabbitMQ

    /// <summary>
    /// Процент для throttling (50% = начинать замедление при заполнении 50% лимита)
    /// </summary>
    public int ThrottlingPercentage { get; set; } = 50;

    /// <summary>
    /// Максимальное количество попыток обработки сообщения
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Базовая задержка между попытками (мс)
    /// </summary>
    public int RetryBaseDelayMs { get; set; } = 1000;

    /// <summary>
    /// Множитель для exponential backoff
    /// 1 = фиксированная задержка, 2 = удвоение каждый раз
    /// </summary>
    public double RetryDelayMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Максимальная задержка между попытками (мс)
    /// </summary>
    public int RetryMaxDelayMs { get; set; } = 30000;

    /// <summary>
    /// Использовать requeue для повторных попыток
    /// false = использовать повторную публикацию с задержкой
    /// </summary>
    public bool UseRequeueForRetries { get; set; } = false;

    /// <summary>
    /// Использовать транзакции для атомарности операций
    /// </summary>
    public bool UseTransactions { get; set; } = false;

    /// <summary>
    /// Включить Dead Letter Queue
    /// </summary>
    public bool EnableDeadLetter { get; set; } = true;

    /// <summary>
    /// Exchange для Dead Letter Queue
    /// </summary>
    public string DeadLetterExchange { get; set; } = "dlx";

    /// <summary>
    /// Routing key для Dead Letter Queue
    /// </summary>
    public string DeadLetterRoutingKey { get; set; } = "dead.letters";

    #endregion

    #region Methods

    internal void Validate()
    {
        if (MaxRetryAttempts < 0)
        {
            throw new InvalidOperationException($"MaxRetryAttempts не может быть отрицательным: {MaxRetryAttempts}");
        }

        if (RetryBaseDelayMs <= 0)
        {
            throw new InvalidOperationException($"RetryBaseDelayMs должен быть больше 0: {RetryBaseDelayMs}");
        }

        if (RetryDelayMultiplier < 1.0)
        {
            throw new InvalidOperationException($"RetryDelayMultiplier должен быть >= 1.0: {RetryDelayMultiplier}");
        }

        if (RetryMaxDelayMs < RetryBaseDelayMs)
        {
            throw new InvalidOperationException("RetryMaxDelayMs должен быть >= RetryBaseDelayMs");
        }
    }

    #endregion

}
