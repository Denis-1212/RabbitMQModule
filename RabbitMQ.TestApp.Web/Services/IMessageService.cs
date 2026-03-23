namespace RabbitMQ.TestApp.Web.Services;

using Models;

public interface IMessageService
{

    #region Methods

    Task<GenericMessage> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default);

    #endregion

}
