namespace RabbitMQ.Module.Infrastructure;

using Client;

public interface IChannelPool : IAsyncDisposable
{

    #region Methods

    Task<IChannel> GetAsync(CancellationToken cancellationToken = default);
    Task ReturnAsync(IChannel channel);

    #endregion

}
