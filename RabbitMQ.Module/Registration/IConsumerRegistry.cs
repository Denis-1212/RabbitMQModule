namespace RabbitMQ.Module.Registration;

public interface IConsumerRegistry
{

    #region Properties

    bool HasRegistrations { get; }

    #endregion

    #region Methods

    void AddRegistration(ConsumerRegistration registration);
    IEnumerable<ConsumerRegistration> GetRegistrationsForMessage(Type messageType);
    IEnumerable<ConsumerRegistration> GetAllRegistrations();

    #endregion

}
