namespace RabbitMQ.TestApp.Console;

using Handlers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Models;

using Module;
using Module.Configuration;
using Module.Contracts;

using Services;

using Console = System.Console;

public class Program
{

    #region Methods

    public static async Task Main(string[] args)
    {
        Console.WriteLine("RabbitMQ Test Console Application");
        Console.WriteLine("=====================================");

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        // Настраиваем логирование
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddSimpleConsole(options =>
        {
            options.IncludeScopes = true;
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        });

        // Читаем настройки
        int messageSendingPeriod = builder.Configuration.GetValue("RabbitMQ:MessageSendingPeriod", 5);
        string connectionString = builder.Configuration.GetValue<string>("RabbitMQ:ConnectionString")
                                  ?? "amqp://guest:guest@localhost:5672/";

        // Регистрируем модуль через DI фабрику
        builder.Services.AddSingleton<MessagingModule>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            // Создаем модуль
            var module = MessagingModule.Create(
                options =>
                {
                    options.ConnectionString = connectionString;
                    options.ClientProvidedName = "TestConsoleApp";

                    // Настройки контроля доставки
                    options.DeliveryControl.PublisherConfirmsEnabled = true;
                    options.DeliveryControl.PublishConfirmationTimeoutMs = 3000;
                    options.DeliveryControl.MaxRetryAttempts = 3;
                    options.DeliveryControl.RetryBaseDelayMs = 1000;
                    options.DeliveryControl.RetryDelayMultiplier = 2.0;
                    options.DeliveryControl.EnableDeadLetter = true;
                },
                loggerFactory,
                sp);

            // Регистрируем потребителя
            module
                .AddConsumer<OrderCreated, OrderCreatedHandler>(c =>
                {
                    c.QueueName = "orders.new";
                    c.PrefetchCount = 1;
                    c.Durable = true;
                    c.AutoDelete = false;
                    c.DeadLetter = new DeadLetterOptions
                    {
                        Exchange = "dlx.orders",
                        RoutingKey = "dead.orders",
                        MaxRetries = 3
                    };
                });

            return module;
        });

        // Регистрируем Publisher 
        builder.Services.AddSingleton<IPublisher>(sp =>
            sp.GetRequiredService<MessagingModule>().CreatePublisher());

        // Регистрируем обработчики
        builder.Services.AddTransient<OrderCreatedHandler>();
        builder.Services.AddSingleton<MessageGenerator>();

        IHost host = builder.Build();

        // Получаем сервисы
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        var module = host.Services.GetRequiredService<MessagingModule>();
        var messageGenerator = host.Services.GetRequiredService<MessageGenerator>();
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

        // Запускаем потребителей
        await module.StartConsumersAsync(lifetime.ApplicationStopping);
        logger.LogInformation("Потребители запущены");

        // Запускаем хост
        await host.StartAsync(lifetime.ApplicationStopping);
        logger.LogInformation("Приложение запущено. Отправка сообщений каждые {Period} секунд", messageSendingPeriod);

        // Запускаем периодическую отправку
        try
        {
            await messageGenerator.RunAsync(5, lifetime.ApplicationStopping);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Цикл отправки остановлен");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка в цикле отправки");
        }
        finally
        {
            logger.LogInformation("Остановка приложения...");
            await host.StopAsync(lifetime.ApplicationStopping);
            await module.DisposeAsync();
            logger.LogInformation("Приложение остановлено");
        }
    }

    #endregion

}
