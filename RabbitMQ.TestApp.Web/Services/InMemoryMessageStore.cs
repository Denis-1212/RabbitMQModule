namespace RabbitMQ.TestApp.Web.Services;

using System.Collections.Concurrent;

using Models;

public class InMemoryMessageStore(IConfiguration configuration) : IMessageStore
{

    #region Fields

    private readonly ConcurrentQueue<ProcessedMessage> _messages = new();
    private readonly ConcurrentDictionary<string, ProcessedMessage> _messageById = new();
    private readonly int _maxSize = configuration.GetValue("WebApp:MaxStoredMessages", 100);
    private readonly object _lock = new();

    #endregion

    #region Methods

    public void Add(ProcessedMessage message)
    {
        int instanceId = GetHashCode();
        Console.WriteLine($"ADD called on instance {instanceId}, message {message.Id}, queue size before: {_messages.Count}");

        lock (_lock)
        {
            _messages.Enqueue(message);
            _messageById[message.Id] = message;

            // Удаляем старые сообщения при превышении лимита
            while (_messages.Count > _maxSize && _messages.TryDequeue(out ProcessedMessage? old))
            {
                _messageById.TryRemove(old.Id, out _);
            }
        }
    }

    public IReadOnlyList<ProcessedMessage> GetRecent(int count)
    {
        int instanceId = GetHashCode();
        Console.WriteLine($"GET on instance {instanceId}, queue size: {_messages.Count}");
        return _messages.Reverse().Take(count).ToList();
    }

    public ProcessedMessage? GetById(string id)
    {
        _messageById.TryGetValue(id, out ProcessedMessage? message);
        return message;
    }

    public void Clear()
    {
        lock (_lock)
        {
            _messages.Clear();
            _messageById.Clear();
        }
    }

    #endregion

}
