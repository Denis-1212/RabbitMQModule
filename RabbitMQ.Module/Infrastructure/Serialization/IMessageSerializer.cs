namespace RabbitMQ.Module.Infrastructure.Serialization;

public interface IMessageSerializer
{

    #region Methods

    byte[] Serialize<T>(T obj);
    T Deserialize<T>(byte[] data);
    object Deserialize(byte[] data, Type type);

    #endregion

}
