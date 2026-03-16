namespace RabbitMQ.Module.Registration;

using System.Collections.Concurrent;

public class ConsumerRegistry : IConsumerRegistry
{

    #region Fields

    private readonly ConcurrentDictionary<Type, List<ConsumerRegistration>> _registrations = new();
    private readonly List<ConsumerRegistration> _allRegistrations = [];

    #endregion

    #region Properties

    public bool HasRegistrations => _allRegistrations.Any();

    #endregion

    #region Methods

    public void AddRegistration(ConsumerRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        _allRegistrations.Add(registration);

        List<ConsumerRegistration> handlers = _registrations.GetOrAdd(registration.MessageType, _ => []);

        lock (handlers)
        {
            handlers.Add(registration);
        }
    }

    public IEnumerable<ConsumerRegistration> GetRegistrationsForMessage(Type messageType)
    {
        if (_registrations.TryGetValue(messageType, out List<ConsumerRegistration>? handlers))
        {
            return handlers.ToList();
        }

        return Enumerable.Empty<ConsumerRegistration>();
    }

    public IEnumerable<ConsumerRegistration> GetAllRegistrations()
    {
        return _allRegistrations.ToList();
    }

    #endregion

}
