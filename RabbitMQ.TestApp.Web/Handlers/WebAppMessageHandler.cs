namespace RabbitMQ.TestApp.Web.Handlers;

using Models;

using Module.Contracts;

using Services;

public class WebAppMessageHandler : IMessageHandler<GenericMessage>
{

    #region Fields

    private readonly ILogger<WebAppMessageHandler> _logger;
    private readonly IMessageStore _messageStore;

    #endregion

    #region Constructors

    public WebAppMessageHandler(ILogger<WebAppMessageHandler> logger, IMessageStore messageStore)
    {
        _logger = logger;
        _messageStore = messageStore;
    }

    #endregion

    #region Methods

    public async Task HandleAsync(GenericMessage message, IMessageContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔥 ОБРАБОТЧИК ВЫЗВАН! MessageId: {MessageId}", message.Id);

        try
        {
            await Task.Delay(50, cancellationToken);

            _logger.LogInformation("📤 Ack для сообщения {MessageId}", message.Id);
            await context.AckAsync(cancellationToken);
            _logger.LogInformation("✅ Ack выполнен для сообщения {MessageId}", message.Id);

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

            _messageStore.Add(processedMessage);

            _logger.LogInformation("✅ Сообщение {MessageId} обработано", message.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка обработки сообщения {MessageId}", message.Id);
            throw;
        }
    }

    #endregion

}
