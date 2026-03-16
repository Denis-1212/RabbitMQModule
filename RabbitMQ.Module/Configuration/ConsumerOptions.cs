namespace RabbitMQ.Module.Configuration;

using System.Text.RegularExpressions;

/// <summary>
/// Настройки потребителя сообщений
/// </summary>
public class ConsumerOptions
{

    #region Fields

    private string _queueName = string.Empty;
    private string? _exchangeName;
    private string? _routingKey;
    private ushort _prefetchCount = 1;
    private int? _maxConcurrentCalls;
    private TimeSpan? _timeout;

    #endregion

    #region Properties

    /// <summary>
    /// Имя очереди для потребления (обязательно)
    /// </summary>
    public string QueueName
    {
        get => _queueName;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("QueueName не может быть пустым", nameof(value));
            }

            if (value.Length > 255)
            {
                throw new ArgumentException($"QueueName слишком длинный (макс. 255 символов): {value}", nameof(value));
            }

            if (!Regex.IsMatch(value, @"^[a-zA-Z0-9\-_\.]+$"))
            {
                throw new ArgumentException($"QueueName содержит недопустимые символы. Допустимы: a-z, A-Z, 0-9, -, _, . : {value}", nameof(value));
            }

            _queueName = value;
        }
    }

    /// <summary>
    /// Имя exchange для привязки (опционально)
    /// </summary>
    public string? ExchangeName
    {
        get => _exchangeName;
        set
        {
            if (value != null && value.Length > 255)
            {
                throw new ArgumentException($"ExchangeName слишком длинный (макс. 255 символов): {value}", nameof(value));
            }

            _exchangeName = value;
        }
    }

    /// <summary>
    /// Routing key для привязки (опционально)
    /// </summary>
    public string? RoutingKey
    {
        get => _routingKey;
        set
        {
            if (value != null && value.Length > 255)
            {
                throw new ArgumentException($"RoutingKey слишком длинный (макс. 255 символов): {value}", nameof(value));
            }

            _routingKey = value;
        }
    }

    /// <summary>
    /// Количество сообщений, выдаваемых потребителю за раз
    /// По умолчанию: 1 (справедливое распределение)
    /// Диапазон: 1-1000
    /// </summary>
    public ushort PrefetchCount
    {
        get => _prefetchCount;
        set
        {
            if (value == 0)
            {
                throw new ArgumentException("PrefetchCount должен быть больше 0", nameof(value));
            }

            if (value > 1000)
            {
                throw new ArgumentException($"PrefetchCount не может превышать 1000: {value}", nameof(value));
            }

            _prefetchCount = value;
        }
    }

    /// <summary>
    /// Долговечная очередь (сохраняется после перезапуска RabbitMQ)
    /// По умолчанию: true
    /// </summary>
    public bool Durable { get; set; } = true;

    /// <summary>
    /// Эксклюзивная очередь (используется только текущим соединением)
    /// По умолчанию: false
    /// </summary>
    public bool Exclusive { get; set; }

    /// <summary>
    /// Авто-удаление очереди (удаляется когда отписывается последний потребитель)
    /// По умолчанию: false
    /// </summary>
    public bool AutoDelete { get; set; }

    /// <summary>
    /// Аргументы очереди (для DLX, TTL, Max Length и т.д.)
    /// </summary>
    public Dictionary<string, object>? Arguments { get; set; }

    /// <summary>
    /// Автоматическое подтверждение сообщений
    /// По умолчанию: false (рекомендуется ручное подтверждение)
    /// </summary>
    public bool AutoAck { get; set; }

    /// <summary>
    /// Максимальное количество параллельных вызовов обработчика
    /// По умолчанию: null (не ограничено)
    /// </summary>
    public int? MaxConcurrentCalls
    {
        get => _maxConcurrentCalls;
        set
        {
            if (value.HasValue && value.Value <= 0)
            {
                throw new ArgumentException("MaxConcurrentCalls должен быть больше 0", nameof(value));
            }

            _maxConcurrentCalls = value;
        }
    }

    /// <summary>
    /// Таймаут обработки сообщения
    /// По умолчанию: null (бесконечно)
    /// </summary>
    public TimeSpan? Timeout
    {
        get => _timeout;
        set
        {
            if (value.HasValue && value.Value <= TimeSpan.Zero)
            {
                throw new ArgumentException("Timeout должен быть положительным", nameof(value));
            }

            _timeout = value;
        }
    }

    /// <summary>
    /// Режим единственного активного потребителя
    /// По умолчанию: false
    /// </summary>
    public bool SingleActiveConsumer { get; set; }

    /// <summary>
    /// Аргументы для Dead Letter Exchange
    /// </summary>
    public DeadLetterOptions? DeadLetter { get; set; }

    /// <summary>
    /// Аргументы для TTL очереди
    /// </summary>
    public QueueTtlOptions? Ttl { get; set; }

    /// <summary>
    /// Аргументы для ограничения длины очереди
    /// </summary>
    public QueueLengthOptions? Length { get; set; }

    #endregion

    #region Methods

    /// <summary>
    /// Валидация параметров конфигурации
    /// </summary>
    internal void Validate()
    {
        var errors = new List<string>();

        // QueueName уже валидируется в setter
        if (string.IsNullOrWhiteSpace(_queueName))
        {
            errors.Add("QueueName не может быть пустым");
        }

        // Проверка ExchangeName если указан
        if (!string.IsNullOrEmpty(_exchangeName) && _exchangeName.Length > 255)
        {
            errors.Add($"ExchangeName слишком длинный (макс. 255 символов): {_exchangeName}");
        }

        // Проверка RoutingKey если указан
        if (!string.IsNullOrEmpty(_routingKey) && _routingKey.Length > 255)
        {
            errors.Add($"RoutingKey слишком длинный (макс. 255 символов): {_routingKey}");
        }

        // Если указан ExchangeName, но не указан RoutingKey - предупреждение
        if (!string.IsNullOrEmpty(_exchangeName) && string.IsNullOrEmpty(_routingKey))
        {
            // Не ошибка, но можно залогировать предупреждение
        }

        // Проверка аргументов
        if (Arguments != null)
        {
            foreach (string key in Arguments.Keys)
            {
                if (string.IsNullOrEmpty(key))
                {
                    errors.Add("Ключ аргумента не может быть пустым");
                }
            }
        }

        // Валидация вложенных опций
        DeadLetter?.Validate();
        Ttl?.Validate();
        Length?.Validate();

        if (errors.Any())
        {
            throw new InvalidOperationException(
                $"Ошибки валидации ConsumerOptions для очереди '{_queueName}':\n{string.Join("\n", errors.Select(e => $"  - {e}"))}");
        }
    }

    /// <summary>
    /// Создает словарь аргументов очереди
    /// </summary>
    internal Dictionary<string, object>? CreateQueueArguments()
    {
        var args = new Dictionary<string, object>();

        // Добавляем пользовательские аргументы
        if (Arguments != null)
        {
            foreach (KeyValuePair<string, object> kvp in Arguments)
            {
                args[kvp.Key] = kvp.Value;
            }
        }

        // Добавляем Dead Letter аргументы
        if (DeadLetter != null)
        {
            DeadLetter.ApplyTo(args);
        }

        // Добавляем TTL аргументы
        if (Ttl != null)
        {
            Ttl.ApplyTo(args);
        }

        // Добавляем Length аргументы
        if (Length != null)
        {
            Length.ApplyTo(args);
        }

        return args.Any() ? args : null;
    }

    /// <summary>
    /// Создает копию текущих настроек
    /// </summary>
    internal ConsumerOptions Clone()
    {
        return new ConsumerOptions
        {
            _queueName = _queueName,
            _exchangeName = _exchangeName,
            _routingKey = _routingKey,
            _prefetchCount = _prefetchCount,
            Durable = Durable,
            Exclusive = Exclusive,
            AutoDelete = AutoDelete,
            AutoAck = AutoAck,
            _maxConcurrentCalls = _maxConcurrentCalls,
            _timeout = _timeout,
            SingleActiveConsumer = SingleActiveConsumer,
            Arguments = Arguments != null
                            ? new Dictionary<string, object>(Arguments)
                            : null,
            DeadLetter = DeadLetter?.Clone(),
            Ttl = Ttl?.Clone(),
            Length = Length?.Clone()
        };
    }

    #endregion

}

/// <summary>
/// Настройки Dead Letter Exchange
/// </summary>
public class DeadLetterOptions
{

    #region Properties

    /// <summary>
    /// Exchange для недоставленных сообщений
    /// </summary>
    public string Exchange { get; set; } = string.Empty;

    /// <summary>
    /// Routing key для недоставленных сообщений
    /// </summary>
    public string RoutingKey { get; set; } = string.Empty;

    /// <summary>
    /// Максимальное количество републикаций
    /// </summary>
    public int? MaxRetries { get; set; }

    #endregion

    #region Methods

    internal void ApplyTo(Dictionary<string, object> args)
    {
        if (!string.IsNullOrEmpty(Exchange))
        {
            args["x-dead-letter-exchange"] = Exchange;
        }

        if (!string.IsNullOrEmpty(RoutingKey))
        {
            args["x-dead-letter-routing-key"] = RoutingKey;
        }

        if (MaxRetries.HasValue)
        {
            // Для реализации повторных попыток через DLQ
            args["x-max-retries"] = MaxRetries.Value;
        }
    }

    internal void Validate()
    {
        if (string.IsNullOrEmpty(Exchange))
        {
            throw new InvalidOperationException("DeadLetter Exchange не может быть пустым");
        }

        if (MaxRetries.HasValue && (MaxRetries.Value <= 0 || MaxRetries.Value > 100))
        {
            throw new InvalidOperationException($"MaxRetries должен быть от 1 до 100: {MaxRetries}");
        }
    }

    internal DeadLetterOptions Clone()
    {
        return new DeadLetterOptions
        {
            Exchange = Exchange,
            RoutingKey = RoutingKey,
            MaxRetries = MaxRetries
        };
    }

    #endregion

}

/// <summary>
/// Настройки TTL очереди
/// </summary>
public class QueueTtlOptions
{

    #region Properties

    /// <summary>
    /// Время жизни сообщений в очереди (миллисекунды)
    /// </summary>
    public int? MessageTtl { get; set; }

    /// <summary>
    /// Время жизни очереди (миллисекунды)
    /// </summary>
    public int? QueueTtl { get; set; }

    #endregion

    #region Methods

    internal void ApplyTo(Dictionary<string, object> args)
    {
        if (MessageTtl.HasValue)
        {
            args["x-message-ttl"] = MessageTtl.Value;
        }

        if (QueueTtl.HasValue)
        {
            args["x-expires"] = QueueTtl.Value;
        }
    }

    internal void Validate()
    {
        if (MessageTtl.HasValue && MessageTtl.Value <= 0)
        {
            throw new InvalidOperationException($"MessageTtl должен быть больше 0: {MessageTtl}");
        }

        if (QueueTtl.HasValue && QueueTtl.Value <= 0)
        {
            throw new InvalidOperationException($"QueueTtl должен быть больше 0: {QueueTtl}");
        }
    }

    internal QueueTtlOptions Clone()
    {
        return new QueueTtlOptions
        {
            MessageTtl = MessageTtl,
            QueueTtl = QueueTtl
        };
    }

    #endregion

}

/// <summary>
/// Настройки ограничения длины очереди
/// </summary>
public class QueueLengthOptions
{

    #region Properties

    /// <summary>
    /// Максимальное количество сообщений в очереди
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Максимальный размер очереди в байтах
    /// </summary>
    public int? MaxLengthBytes { get; set; }

    /// <summary>
    /// Поведение при превышении лимита (drop-head/reject-publish)
    /// </summary>
    public string? OverflowStrategy { get; set; }

    #endregion

    #region Methods

    internal void ApplyTo(Dictionary<string, object> args)
    {
        if (MaxLength.HasValue)
        {
            args["x-max-length"] = MaxLength.Value;
        }

        if (MaxLengthBytes.HasValue)
        {
            args["x-max-length-bytes"] = MaxLengthBytes.Value;
        }

        if (!string.IsNullOrEmpty(OverflowStrategy))
        {
            args["x-overflow"] = OverflowStrategy;
        }
    }

    internal void Validate()
    {
        if (MaxLength.HasValue && MaxLength.Value <= 0)
        {
            throw new InvalidOperationException($"MaxLength должен быть больше 0: {MaxLength}");
        }

        if (MaxLengthBytes.HasValue && MaxLengthBytes.Value <= 0)
        {
            throw new InvalidOperationException($"MaxLengthBytes должен быть больше 0: {MaxLengthBytes}");
        }

        if (!string.IsNullOrEmpty(OverflowStrategy) &&
            OverflowStrategy != "drop-head" &&
            OverflowStrategy != "reject-publish")
        {
            throw new InvalidOperationException($"OverflowStrategy должен быть 'drop-head' или 'reject-publish': {OverflowStrategy}");
        }
    }

    internal QueueLengthOptions Clone()
    {
        return new QueueLengthOptions
        {
            MaxLength = MaxLength,
            MaxLengthBytes = MaxLengthBytes,
            OverflowStrategy = OverflowStrategy
        };
    }

    #endregion

}
