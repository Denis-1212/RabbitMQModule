namespace RabbitMQ.TestApp.Console.Handlers;

using Microsoft.Extensions.Logging;

using Models;

using Module.Contracts;

public class OrderCreatedHandler(ILogger<OrderCreatedHandler> logger) : IMessageHandler<OrderCreated>
{

    #region Fields

    private static readonly Random _random = new();

    #endregion

    #region Methods

    public async Task HandleAsync(OrderCreated message, IMessageContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Получен заказ: {Order}", message);

        // Имитация бизнес-логики с случайными ошибками
        try
        {
            // 20% ошибок для демонстрации retry
            if (_random.Next(100) < 20)
            {
                throw new InvalidOperationException("Случайная ошибка обработки заказа");
            }

            // Имитация обработки
            await Task.Delay(_random.Next(500, 1500), cancellationToken);

            logger.LogInformation("Заказ обработан: {OrderId}", message.OrderId);
            await context.AckAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger.LogError(ex, "Ошибка обработки заказа {OrderId}", message.OrderId);
            throw; // Retry для технических ошибок
        }
    }

    #endregion

}
