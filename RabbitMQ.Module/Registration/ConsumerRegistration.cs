namespace RabbitMQ.Module.Registration;

using System.Collections;
using System.Collections.Concurrent;

using Configuration;

using Contracts;

/// <summary>
/// Регистрация потребителя сообщений
/// </summary>
public class ConsumerRegistration
{

    #region Properties

    /// <summary>
    /// Тип сообщения, которое обрабатывает потребитель
    /// </summary>
    public Type MessageType { get; }

    /// <summary>
    /// Тип обработчика сообщений
    /// </summary>
    public Type HandlerType { get; }

    /// <summary>
    /// Настройки потребителя
    /// </summary>
    public ConsumerOptions Options { get; }

    /// <summary>
    /// Уникальный идентификатор регистрации (генерируется автоматически)
    /// </summary>
    public string RegistrationId { get; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Время создания регистрации
    /// </summary>
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    /// <summary>
    /// Теги потребителя (для RabbitMQ consumer tags)
    /// </summary>
    public List<string> ConsumerTags { get; } = new();

    /// <summary>
    /// Метаданные регистрации
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = new();

    /// <summary>
    /// Флаг активности потребителя
    /// </summary>
    public bool IsActive { get; internal set; }

    /// <summary>
    /// Статистика потребителя
    /// </summary>
    public ConsumerStats Stats { get; } = new();

    #endregion

    #region Constructors

    /// <summary>
    /// Создает новую регистрацию потребителя
    /// </summary>
    /// <param name = "messageType">Тип сообщения</param>
    /// <param name = "handlerType">Тип обработчика</param>
    /// <param name = "options">Настройки потребителя</param>
    /// <exception cref = "ArgumentNullException">Если любой параметр null</exception>
    public ConsumerRegistration(
        Type messageType,
        Type handlerType,
        ConsumerOptions options)
    {
        MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType));
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
        Options = options ?? throw new ArgumentNullException(nameof(options));

        Validate();
    }

    #endregion

    #region Methods

    /// <summary>
    /// Создает копию регистрации
    /// </summary>
    public ConsumerRegistration Clone()
    {
        var clone = new ConsumerRegistration(
            MessageType,
            HandlerType,
            Options.Clone())
        {
            IsActive = IsActive
        };

        clone.ConsumerTags.AddRange(ConsumerTags);

        foreach (KeyValuePair<string, object> kvp in Metadata)
        {
            clone.Metadata[kvp.Key] = kvp.Value;
        }

        return clone;
    }

    /// <summary>
    /// Возвращает строковое представление регистрации
    /// </summary>
    public override string ToString()
    {
        return $"ConsumerRegistration [Id: {RegistrationId}, " +
               $"Message: {MessageType.Name}, " +
               $"Handler: {HandlerType.Name}, " +
               $"Queue: {Options.QueueName}, " +
               $"Active: {IsActive}]";
    }

    /// <summary>
    /// Проверяет валидность регистрации
    /// </summary>
    private void Validate()
    {
        var errors = new List<string>();

        // Проверка типа сообщения
        if (!MessageType.IsClass || MessageType.IsAbstract)
        {
            errors.Add($"Тип сообщения {MessageType.Name} должен быть конкретным классом");
        }

        // Проверка типа обработчика
        if (!HandlerType.IsClass || HandlerType.IsAbstract)
        {
            errors.Add($"Тип обработчика {HandlerType.Name} должен быть конкретным классом");
        }

        // Проверка реализации интерфейса IMessageHandler<>
        Type handlerInterface = typeof(IMessageHandler<>).MakeGenericType(MessageType);

        if (!handlerInterface.IsAssignableFrom(HandlerType))
        {
            errors.Add($"Тип обработчика {HandlerType.Name} должен реализовывать IMessageHandler<{MessageType.Name}>");
        }

        // Проверка наличия публичного конструктора
        if (HandlerType.GetConstructors().All(c => !c.IsPublic))
        {
            errors.Add($"Тип обработчика {HandlerType.Name} должен иметь публичный конструктор");
        }

        // Валидация опций
        try
        {
            Options.Validate();
        }
        catch (InvalidOperationException ex)
        {
            errors.Add($"Ошибка в настройках: {ex.Message}");
        }

        if (errors.Any())
        {
            throw new InvalidOperationException(
                $"Ошибки валидации ConsumerRegistration для {MessageType.Name} -> {HandlerType.Name}:\n" +
                string.Join("\n", errors.Select(e => $"  - {e}")));
        }
    }

    #endregion

}

/// <summary>
/// Статистика потребителя
/// </summary>
public class ConsumerStats
{

    #region Fields

    private long _messagesReceived;
    private long _messagesSucceeded;
    private long _messagesFailed;
    private long _messagesRetried;
    private readonly ConcurrentDictionary<string, long> _errors = new();

    #endregion

    #region Properties

    /// <summary>
    /// Количество полученных сообщений
    /// </summary>
    public long MessagesReceived => Interlocked.Read(ref _messagesReceived);

    /// <summary>
    /// Количество успешно обработанных сообщений
    /// </summary>
    public long MessagesSucceeded => Interlocked.Read(ref _messagesSucceeded);

    /// <summary>
    /// Количество сообщений с ошибкой
    /// </summary>
    public long MessagesFailed => Interlocked.Read(ref _messagesFailed);

    /// <summary>
    /// Количество повторных попыток
    /// </summary>
    public long MessagesRetried => Interlocked.Read(ref _messagesRetried);

    /// <summary>
    /// Время последнего полученного сообщения
    /// </summary>
    public DateTime? LastMessageReceivedAt { get; private set; }

    /// <summary>
    /// Время последней успешной обработки
    /// </summary>
    public DateTime? LastSuccessAt { get; private set; }

    /// <summary>
    /// Время последней ошибки
    /// </summary>
    public DateTime? LastErrorAt { get; private set; }

    /// <summary>
    /// Карта ошибок с количеством
    /// </summary>
    public IReadOnlyDictionary<string, long> Errors => _errors;

    #endregion

    #region Methods

    /// <summary>
    /// Возвращает строковое представление статистики
    /// </summary>
    public override string ToString()
    {
        return $"Received: {MessagesReceived}, " +
               $"Succeeded: {MessagesSucceeded}, " +
               $"Failed: {MessagesFailed}, " +
               $"Retried: {MessagesRetried}, " +
               $"Errors: {_errors.Count} types";
    }

    /// <summary>
    /// Увеличить счетчик полученных сообщений
    /// </summary>
    internal void IncrementReceived()
    {
        Interlocked.Increment(ref _messagesReceived);
        LastMessageReceivedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Увеличить счетчик успешных сообщений
    /// </summary>
    internal void IncrementSucceeded()
    {
        Interlocked.Increment(ref _messagesSucceeded);
        LastSuccessAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Увеличить счетчик сообщений с ошибкой
    /// </summary>
    internal void IncrementFailed(string? errorType = null)
    {
        Interlocked.Increment(ref _messagesFailed);
        LastErrorAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(errorType))
        {
            _errors.AddOrUpdate(errorType, 1, (_, count) => count + 1);
        }
    }

    /// <summary>
    /// Увеличить счетчик повторных попыток
    /// </summary>
    internal void IncrementRetried()
    {
        Interlocked.Increment(ref _messagesRetried);
    }

    /// <summary>
    /// Сбросить статистику
    /// </summary>
    internal void Reset()
    {
        Interlocked.Exchange(ref _messagesReceived, 0);
        Interlocked.Exchange(ref _messagesSucceeded, 0);
        Interlocked.Exchange(ref _messagesFailed, 0);
        Interlocked.Exchange(ref _messagesRetried, 0);
        _errors.Clear();
        LastMessageReceivedAt = null;
        LastSuccessAt = null;
        LastErrorAt = null;
    }

    #endregion

}

/// <summary>
/// Коллекция регистраций потребителей
/// </summary>
public class ConsumerRegistrationCollection : IEnumerable<ConsumerRegistration>
{

    #region Fields

    private readonly ConcurrentDictionary<string, ConsumerRegistration> _registrations = new();
    private readonly ConcurrentDictionary<Type, List<string>> _messageTypeIndex = new();
    private readonly ReaderWriterLockSlim _lock = new();

    #endregion

    #region Properties

    /// <summary>
    /// Количество регистраций
    /// </summary>
    public int Count => _registrations.Count;

    #endregion

    #region Methods

    /// <summary>
    /// Добавить регистрацию
    /// </summary>
    public void Add(ConsumerRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (!_registrations.TryAdd(registration.RegistrationId, registration))
        {
            throw new InvalidOperationException($"Регистрация с ID {registration.RegistrationId} уже существует");
        }

        _lock.EnterWriteLock();

        try
        {
            if (!_messageTypeIndex.TryGetValue(registration.MessageType, out List<string>? list))
            {
                list = new List<string>();
                _messageTypeIndex[registration.MessageType] = list;
            }

            list.Add(registration.RegistrationId);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Удалить регистрацию
    /// </summary>
    public bool Remove(string registrationId)
    {
        if (_registrations.TryRemove(registrationId, out ConsumerRegistration? registration))
        {
            _lock.EnterWriteLock();

            try
            {
                if (_messageTypeIndex.TryGetValue(registration.MessageType, out List<string>? list))
                {
                    list.Remove(registrationId);

                    if (list.Count == 0)
                    {
                        _messageTypeIndex.TryRemove(registration.MessageType, out _);
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Получить регистрацию по ID
    /// </summary>
    public ConsumerRegistration? Get(string registrationId)
    {
        return _registrations.TryGetValue(registrationId, out ConsumerRegistration? registration) ? registration : null;
    }

    /// <summary>
    /// Получить все регистрации для типа сообщения
    /// </summary>
    public IEnumerable<ConsumerRegistration> GetForMessageType(Type messageType)
    {
        _lock.EnterReadLock();

        try
        {
            if (_messageTypeIndex.TryGetValue(messageType, out List<string>? ids))
            {
                return ids
                    .Select(id => _registrations.TryGetValue(id, out ConsumerRegistration? reg) ? reg : null)
                    .Where(reg => reg != null)
                    .Select(reg => reg!)
                    .ToList();
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }

        return Enumerable.Empty<ConsumerRegistration>();
    }

    /// <summary>
    /// Получить все регистрации
    /// </summary>
    public IEnumerable<ConsumerRegistration> GetAll()
    {
        return _registrations.Values.ToList();
    }

    /// <inheritdoc/>
    public IEnumerator<ConsumerRegistration> GetEnumerator()
    {
        return _registrations.Values.GetEnumerator();
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    #endregion

}
