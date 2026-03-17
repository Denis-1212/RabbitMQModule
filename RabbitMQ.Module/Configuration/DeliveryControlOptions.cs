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

    #endregion

}
