namespace RabbitMQ.Module.Messaging;

using Client;

using Contracts;

internal class MessageContext(
    string messageId,
    string routingKey,
    DateTime timestamp,
    ulong deliveryTag,
    IChannel channel,
    CancellationToken cancellationToken)
    : IMessageContext
{

    #region Fields

    private bool _isAcked;

    #endregion

    #region Properties

    public string MessageId { get; } = messageId;
    public string RoutingKey { get; } = routingKey;
    public DateTime Timestamp { get; } = timestamp;

    #endregion

    #region Methods

    public async Task AckAsync(CancellationToken cancellationToken1 = default)
    {
        if (_isAcked)
        {
            return;
        }

        CancellationToken token = cancellationToken1 == CancellationToken.None ? cancellationToken : cancellationToken1;
        await channel.BasicAckAsync(deliveryTag, false, token);
        _isAcked = true;
    }

    public async Task NackAsync(bool requeue = false, CancellationToken cancellationToken1 = default)
    {
        if (_isAcked)
        {
            return;
        }

        CancellationToken token = cancellationToken1 == CancellationToken.None ? cancellationToken : cancellationToken1;
        await channel.BasicNackAsync(deliveryTag, false, requeue, token);
        _isAcked = true;
    }

    #endregion

}
