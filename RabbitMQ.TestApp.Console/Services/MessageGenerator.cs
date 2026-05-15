namespace RabbitMQ.TestApp.Console.Services;

using Microsoft.Extensions.Logging;

using Models;

using Module.Contracts;

public class MessageGenerator(IPublisher publisher, ILogger<MessageGenerator> logger)
{

    #region Fields

    private readonly Random _random = new();

    private readonly string[] _customers = { "Иван Петров", "Мария Иванова", "Алексей Сидоров", "Елена Смирнова" };
    private readonly string[] _emails = { "user1@test.com", "user2@test.com", "user3@test.com" };

    private readonly string[][] _orderItems = new[]
    {
        new[] { "Ноутбук", "Мышь" }, new[] { "Книга", "Блокнот", "Ручка" }, new[] { "Кофе", "Чай", "Сахар" }, new[] { "Монитор", "Клавиатура" }
    };

    #endregion

    #region Methods

    public async Task RunAsync(int periodTimer, CancellationToken stoppingToken)
    {
        // Запускаем периодическую отправку
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(periodTimer));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SendMessageAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Приложение останавливается...");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при отправке сообщения");
        }
    }

    private async Task SendMessageAsync(CancellationToken stoppingToken)
    {
        var order = new OrderCreated
        {
            CustomerName = _customers[_random.Next(_customers.Length)],
            Amount = _random.Next(1000, 10000) / 100m,
            Items = _orderItems[_random.Next(_orderItems.Length)].ToList()
        };

        logger.LogInformation("Отправка заказа: {Order}", order);

        DateTime startTime = DateTime.UtcNow;

        await publisher.PublishAsync(
            order,
            config =>
            {
                config.WithRoutingKey("orders.new");
                config.WithMandatory();
            },
            stoppingToken);

        TimeSpan latency = DateTime.UtcNow - startTime;
        logger.LogInformation("Заказ отправлен за {Latency}ms", latency.TotalMilliseconds);
    }

    #endregion

}
