namespace RabbitMQ.Module.Infrastructure;

using Client;

public interface IConnectionManager : IAsyncDisposable
{

    #region Methods

    Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default);

    #endregion

}
