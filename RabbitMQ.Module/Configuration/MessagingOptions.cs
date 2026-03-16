namespace RabbitMQ.Module.Configuration;

using System.Diagnostics;
using System.Net.Security;
using System.Text.RegularExpressions;

using Client;

/// <summary>
/// Настройки подключения к RabbitMQ
/// </summary>
public class MessagingOptions
{

    #region Fields

    private string _connectionString = "amqp://guest:guest@localhost:5672/";
    private string _clientProvidedName = "RabbitMQ.Module";
    private string _queuePrefix = string.Empty;

    #endregion

    #region Properties

    /// <summary>
    /// Строка подключения к RabbitMQ в формате AMQP URI
    /// <example>
    /// amqp://guest:guest@localhost:5672/
    /// amqps://user:password@rabbitmq.example.com:5671/vhost
    /// </example>
    /// </summary>
    public string ConnectionString
    {
        get => _connectionString;
        set => _connectionString = value ?? throw new ArgumentNullException(nameof(ConnectionString));
    }

    /// <summary>
    /// Имя клиента для идентификации в RabbitMQ (отображается в Management UI)
    /// </summary>
    public string ClientProvidedName
    {
        get => _clientProvidedName;
        set => _clientProvidedName = value ?? throw new ArgumentNullException(nameof(ClientProvidedName));
    }

    /// <summary>
    /// Интервал heartbeat в секундах для поддержания соединения
    /// Рекомендуемое значение: 60 секунд
    /// Минимальное значение: 10 секунд
    /// Максимальное значение: 300 секунд
    /// </summary>
    public TimeSpan RequestedHeartbeat { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Автоматическое восстановление подключения при сбоях
    /// </summary>
    public bool AutomaticRecoveryEnabled { get; set; } = true;

    /// <summary>
    /// Интервал между попытками восстановления соединения
    /// Диапазон: от 1 секунды до 5 минут
    /// </summary>
    public TimeSpan NetworkRecoveryInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Таймаут подключения к RabbitMQ
    /// Диапазон: от 1 секунды до 2 минут
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Максимальное количество каналов (ChannelMax) для подключения
    /// 0 = использовать значение сервера по умолчанию
    /// Максимальное значение: 65535
    /// </summary>
    public ushort RequestedChannelMax { get; set; }

    /// <summary>
    /// Максимальный размер кадра в байтах
    /// 0 = использовать значение сервера по умолчанию
    /// </summary>
    public uint RequestedFrameMax { get; set; }

    /// <summary>
    /// Максимальное количество каналов в пуле (наше внутреннее ограничение)
    /// Диапазон: 1-1000
    /// Рекомендуемое значение: 50
    /// </summary>
    public int MaxChannelsInPool { get; set; } = 50;

    /// <summary>
    /// Включить Publisher Confirms для надежной доставки сообщений
    /// Рекомендуется включать для критичных сообщений
    /// </summary>
    public bool PublisherConfirms { get; set; } = true;

    /// <summary>
    /// Количество попыток подключения при старте
    /// Диапазон: 1-10
    /// </summary>
    public int ConnectionRetryCount { get; set; } = 5;

    /// <summary>
    /// Задержка между попытками подключения (умножается на номер попытки)
    /// </summary>
    public TimeSpan ConnectionRetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Префикс для имен очередей (для изоляции окружений, например "dev", "prod")
    /// Будет добавлен перед именем очереди через точку: {Prefix}.{QueueName}
    /// Допустимые символы: буквы, цифры, дефис, подчеркивание
    /// </summary>
    public string QueuePrefix
    {
        get => _queuePrefix;
        set
        {
            if (!string.IsNullOrEmpty(value) && !Regex.IsMatch(value, @"^[a-zA-Z0-9\-_]+$"))
            {
                throw new ArgumentException("QueuePrefix может содержать только буквы, цифры, дефис и подчеркивание");
            }

            _queuePrefix = value ?? string.Empty;
        }
    }

    /// <summary>
    /// Максимальный размер сообщения в байтах
    /// 0 = не ограничено
    /// Минимальное значение при включении: 1024 байта (1KB)
    /// </summary>
    public uint MaxMessageSize { get; set; }

    /// <summary>
    /// Таймаут на операции с каналом
    /// </summary>
    public TimeSpan ContinuationTimeout { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Использовать ли SSL/TLS для подключения
    /// </summary>
    public bool SslEnabled { get; set; }

    /// <summary>
    /// Настройки SSL сертификата (обязательны при SslEnabled = true)
    /// </summary>
    public SslOption? SslOption { get; set; }

    /// <summary>
    /// Допустимые ошибки SSL политик, которые не приводят к сбою подключения
    /// По умолчанию: RemoteCertificateNameMismatch | RemoteCertificateChainErrors
    /// </summary>
    public SslPolicyErrors AcceptableSslErrors { get; set; } = SslPolicyErrors.RemoteCertificateNameMismatch |
                                                               SslPolicyErrors.RemoteCertificateChainErrors;

    /// <summary>
    /// Виртуальный хост RabbitMQ (извлекается из ConnectionString)
    /// </summary>
    public string VirtualHost => ParseVirtualHost();

    /// <summary>
    /// Имя хоста RabbitMQ (извлекается из ConnectionString)
    /// </summary>
    public string HostName
    {
        get
        {
            try
            {
                var uri = new Uri(_connectionString);
                return uri.Host;
            }
            catch
            {
                return "localhost";
            }
        }
    }

    /// <summary>
    /// Порт RabbitMQ (извлекается из ConnectionString)
    /// </summary>
    public int Port
    {
        get
        {
            try
            {
                var uri = new Uri(_connectionString);
                return uri.Port;
            }
            catch
            {
                return SslEnabled ? 5671 : 5672;
            }
        }
    }

    /// <summary>
    /// Имя пользователя (извлекается из ConnectionString)
    /// </summary>
    public string UserName
    {
        get
        {
            try
            {
                var uri = new Uri(_connectionString);
                string[] userInfo = uri.UserInfo.Split(':');
                return userInfo.Length > 0 ? userInfo[0] : "guest";
            }
            catch
            {
                return "guest";
            }
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Возвращает строковое представление настроек (без чувствительных данных)
    /// </summary>
    public override string ToString()
    {
        try
        {
            var uri = new Uri(_connectionString);
            string[] userInfo = uri.UserInfo.Split(':');
            string userName = userInfo.Length > 0 ? userInfo[0] : "unknown";

            return $"Host: {uri.Host}:{uri.Port}, " +
                   $"VHost: {VirtualHost}, " +
                   $"User: {userName}, " +
                   $"SSL: {SslEnabled}, " +
                   $"Channels in pool: {MaxChannelsInPool}, " +
                   $"ChannelMax: {RequestedChannelMax}, " +
                   $"Heartbeat: {RequestedHeartbeat.TotalSeconds}s, " +
                   $"RetryCount: {ConnectionRetryCount}";
        }
        catch
        {
            return $"ConnectionString: [invalid], SSL: {SslEnabled}";
        }
    }

    /// <summary>
    /// Валидация параметров конфигурации
    /// </summary>
    /// <exception cref = "InvalidOperationException">Выбрасывается при неверных параметрах</exception>
    internal void Validate()
    {
        var errors = new List<string>();

        // Проверка строки подключения
        ValidateConnectionString(errors);

        // Проверка MaxChannelsInPool
        if (MaxChannelsInPool <= 0)
        {
            errors.Add($"MaxChannelsInPool должен быть больше 0. Текущее значение: {MaxChannelsInPool}");
        }
        else if (MaxChannelsInPool > 1000)
        {
            errors.Add($"MaxChannelsInPool не может превышать 1000. Текущее значение: {MaxChannelsInPool}");
        }

        // Проверка ConnectionRetryCount
        if (ConnectionRetryCount <= 0)
        {
            errors.Add($"ConnectionRetryCount должен быть больше 0. Текущее значение: {ConnectionRetryCount}");
        }
        else if (ConnectionRetryCount > 10)
        {
            errors.Add($"ConnectionRetryCount не может превышать 10. Текущее значение: {ConnectionRetryCount}");
        }

        // Проверка RequestedHeartbeat
        if (RequestedHeartbeat < TimeSpan.FromSeconds(10))
        {
            errors.Add($"RequestedHeartbeat должен быть не менее 10 секунд. Текущее значение: {RequestedHeartbeat.TotalSeconds}с");
        }
        else if (RequestedHeartbeat > TimeSpan.FromSeconds(300))
        {
            errors.Add($"RequestedHeartbeat не может превышать 300 секунд. Текущее значение: {RequestedHeartbeat.TotalSeconds}с");
        }

        // Проверка NetworkRecoveryInterval
        if (NetworkRecoveryInterval < TimeSpan.FromSeconds(1))
        {
            errors.Add($"NetworkRecoveryInterval должен быть не менее 1 секунды. Текущее значение: {NetworkRecoveryInterval.TotalSeconds}с");
        }
        else if (NetworkRecoveryInterval > TimeSpan.FromMinutes(5))
        {
            errors.Add($"NetworkRecoveryInterval не может превышать 5 минут. Текущее значение: {NetworkRecoveryInterval.TotalMinutes}мин");
        }

        // Проверка ConnectionTimeout
        if (ConnectionTimeout < TimeSpan.FromSeconds(1))
        {
            errors.Add($"ConnectionTimeout должен быть не менее 1 секунды. Текущее значение: {ConnectionTimeout.TotalSeconds}с");
        }
        else if (ConnectionTimeout > TimeSpan.FromMinutes(2))
        {
            errors.Add($"ConnectionTimeout не может превышать 2 минуты. Текущее значение: {ConnectionTimeout.TotalMinutes}мин");
        }

        // Проверка MaxMessageSize
        if (MaxMessageSize > 0 && MaxMessageSize < 1024)
        {
            errors.Add($"MaxMessageSize должен быть не менее 1024 байт (1KB) или 0 для отключения. Текущее значение: {MaxMessageSize}");
        }

        // Проверка SSL настроек
        if (SslEnabled && SslOption == null)
        {
            errors.Add("SslOption должен быть указан при включенном SSL");
        }

        // Если есть ошибки, выбрасываем исключение
        if (errors.Any())
        {
            throw new InvalidOperationException(
                $"Ошибки валидации MessagingOptions:\n{string.Join("\n", errors.Select(e => $"  - {e}"))}");
        }
    }

    /// <summary>
    /// Создает фабрику подключений на основе настроек
    /// </summary>
    internal ConnectionFactory CreateConnectionFactory()
    {
        var uri = new Uri(_connectionString);

        var factory = new ConnectionFactory
        {
            Uri = uri,
            ClientProvidedName = _clientProvidedName,
            RequestedHeartbeat = RequestedHeartbeat,
            AutomaticRecoveryEnabled = AutomaticRecoveryEnabled,
            NetworkRecoveryInterval = NetworkRecoveryInterval,
            ContinuationTimeout = ContinuationTimeout,
            RequestedChannelMax = RequestedChannelMax,
            RequestedFrameMax = RequestedFrameMax
            // DispatchConsumersAsync удален - не существует в ConnectionFactory
        };

        // Настройка SSL если требуется
        if (SslEnabled)
        {
            factory.Ssl = SslOption ?? new SslOption
            {
                Enabled = true,
                ServerName = uri.Host,
                AcceptablePolicyErrors = AcceptableSslErrors
            };
        }

        return factory;
    }

    /// <summary>
    /// Создает копию текущих настроек
    /// </summary>
    internal MessagingOptions Clone()
    {
        var clone = new MessagingOptions
        {
            _connectionString = _connectionString,
            _clientProvidedName = _clientProvidedName,
            _queuePrefix = _queuePrefix,
            AcceptableSslErrors = AcceptableSslErrors,
            RequestedHeartbeat = RequestedHeartbeat,
            AutomaticRecoveryEnabled = AutomaticRecoveryEnabled,
            NetworkRecoveryInterval = NetworkRecoveryInterval,
            ConnectionTimeout = ConnectionTimeout,
            RequestedChannelMax = RequestedChannelMax,
            RequestedFrameMax = RequestedFrameMax,
            MaxChannelsInPool = MaxChannelsInPool,
            PublisherConfirms = PublisherConfirms,
            ConnectionRetryCount = ConnectionRetryCount,
            ConnectionRetryDelay = ConnectionRetryDelay,
            MaxMessageSize = MaxMessageSize,
            ContinuationTimeout = ContinuationTimeout,
            SslEnabled = SslEnabled
        };

        // Ручное копирование SslOption
        if (SslOption != null)
        {
            clone.SslOption = new SslOption
            {
                Enabled = SslOption.Enabled,
                ServerName = SslOption.ServerName,
                CertPath = SslOption.CertPath,
                CertPassphrase = SslOption.CertPassphrase,
                CertificateValidationCallback = SslOption.CertificateValidationCallback,
                CertificateSelectionCallback = SslOption.CertificateSelectionCallback,
                AcceptablePolicyErrors = SslOption.AcceptablePolicyErrors,
                Version = SslOption.Version,
                Certs = SslOption.Certs // Это коллекция сертификатов вместо Certificate
            };
        }

        return clone;
    }

    private void ValidateConnectionString(List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            errors.Add("ConnectionString не может быть пустым");
            return;
        }

        try
        {
            var uri = new Uri(_connectionString);

            // Проверка схемы
            if (uri.Scheme != "amqp" && uri.Scheme != "amqps")
            {
                errors.Add($"ConnectionString должна использовать amqp:// или amqps://. Текущий протокол: {uri.Scheme}");
            }

            // Проверка наличия логина и пароля
            if (string.IsNullOrEmpty(uri.UserInfo))
            {
                errors.Add("ConnectionString должна содержать логин и пароль в формате user:password@host");
            }
            else if (!uri.UserInfo.Contains(':'))
            {
                errors.Add("ConnectionString должна содержать логин и пароль, разделенные двоеточием");
            }

            // Проверка хоста
            if (string.IsNullOrEmpty(uri.Host))
            {
                errors.Add("ConnectionString должна содержать имя хоста");
            }
        }
        catch (UriFormatException ex)
        {
            errors.Add($"ConnectionString имеет неверный формат URI: {_connectionString}. Ошибка: {ex.Message}");
        }
    }

    private string ParseVirtualHost()
    {
        try
        {
            var uri = new Uri(_connectionString);
            string path = uri.AbsolutePath.TrimStart('/');
            return string.IsNullOrEmpty(path) ? "/" : path;
        }
        catch
        {
            return "/"; // fallback при ошибке парсинга
        }
    }

    #endregion

}

/// <summary>
/// Расширения для MessagingOptions
/// </summary>
public static class MessagingOptionsExtensions
{

    #region Methods

    /// <summary>
    /// Проверяет, доступен ли RabbitMQ по указанным настройкам
    /// </summary>
    /// <param name = "options">Настройки подключения</param>
    /// <param name = "cancellationToken">Токен отмены</param>
    /// <returns>true если подключение успешно, иначе false</returns>
    public static async Task<bool> TestConnectionAsync(this MessagingOptions options, CancellationToken cancellationToken = default)
    {
        try
        {
            ConnectionFactory factory = options.CreateConnectionFactory();
            using IConnection connection = await factory.CreateConnectionAsync(cancellationToken);
            using IChannel channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            // Здесь мы не можем логировать, т.к. нет ILogger
            Debug.WriteLine($"Connection test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Создает строку подключения из отдельных компонентов
    /// </summary>
    public static string BuildConnectionString(
        string host = "localhost",
        int port = 5672,
        string vHost = "/",
        string userName = "guest",
        string password = "guest",
        bool useSsl = false)
    {
        string scheme = useSsl ? "amqps" : "amqp";
        string vHostPart = vHost != "/" ? vHost.TrimStart('/') : "";

        return $"{scheme}://{userName}:{password}@{host}:{port}/{vHostPart}";
    }

    #endregion

}
