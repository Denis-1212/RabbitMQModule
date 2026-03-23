namespace RabbitMQ.TestApp.Web.Services;

using Models;

using Module.Contracts;

public class MessageService : IMessageService
{

    #region Fields

    private readonly IPublisher _publisher;
    private readonly ILogger<MessageService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _routingKey;

    #endregion

    #region Constructors

    public MessageService(
        IPublisher publisher,
        ILogger<MessageService> logger,
        IConfiguration configuration)
    {
        _publisher = publisher;
        _logger = logger;
        _configuration = configuration;
        _routingKey = configuration["RabbitMQ:RoutingKey"] ?? "webapp.messages";
    }

    #endregion

    #region Methods

    public async Task<GenericMessage> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        var message = new GenericMessage
        {
            Text = request.Text,
            Sender = request.Sender ?? "WebApp",
            Metadata = request.Metadata
        };

        _logger.LogInformation("📤 Отправка сообщения {MessageId}: {Text}", message.Id, message.Text);

        await _publisher.PublishAsync(
            message,
            config =>
            {
                config.WithRoutingKey(_routingKey);
                config.WithMandatory();
            },
            cancellationToken);

        _logger.LogInformation(
            "✅ Сообщение {MessageId} отправлено в очередь {Queue}",
            message.Id,
            _configuration["RabbitMQ:QueueName"]);

        return message;
    }

    #endregion

}
