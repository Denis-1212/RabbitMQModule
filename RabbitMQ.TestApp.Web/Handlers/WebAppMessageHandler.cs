namespace RabbitMQ.TestApp.Web.Handlers;

using Models;

using Module.Contracts;

using Services;

public class WebAppMessageHandler(ILogger<WebAppMessageHandler> logger, IMessageStore messageStore) : IMessageHandler<GenericMessage>
{

    #region Methods

    public Task HandleAsync(GenericMessage message, IMessageContext context, CancellationToken cancellationToken)
    {
        try
        {
            // Сохраняем в хранилище
            var processedMessage = new ProcessedMessage
            {
                Id = message.Id,
                Text = message.Text,
                Sender = message.Sender,
                ReceivedAt = message.Timestamp,
                ProcessedAt = DateTime.UtcNow,
                Success = true
            };

            messageStore.Add(processedMessage);

            logger.LogInformation("HandleAsync : Сообщение {MessageId} обработано", message.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка обработки сообщения {MessageId}", message.Id);
            throw;
        }

        return Task.CompletedTask;
    }

    #endregion

}
