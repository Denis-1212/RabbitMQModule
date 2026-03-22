namespace RabbitMQ.TestApp.Web.Extensions;

using Handlers;

using Models;

using Module;

public static class RabbitMQExtensions
{

    #region Methods

    public static IServiceCollection AddRabbitMQModuleWithHandlers(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Читаем настройки
        string connectionString = configuration["RabbitMQ:ConnectionString"]
                                  ?? "amqp://guest:guest@localhost:5672/";

        string queueName = configuration["RabbitMQ:QueueName"] ?? "webapp.messages";

        // Создаем модуль
        var module = MessagingModule.Create(
            options =>
            {
                options.ConnectionString = connectionString;
                options.ClientProvidedName = configuration["RabbitMQ:ClientProvidedName"] ?? "WebApp";
                options.DeliveryControl.PublisherConfirmsEnabled =
                    configuration.GetValue("RabbitMQ:PublisherConfirmsEnabled", true);

                options.DeliveryControl.EnableDeadLetter =
                    configuration.GetValue("RabbitMQ:EnableDeadLetter", true);

                options.DeliveryControl.MaxRetryAttempts =
                    configuration.GetValue("RabbitMQ:MaxRetryAttempts", 3);
            },
            services.BuildServiceProvider().GetRequiredService<ILoggerFactory>(),
            services.BuildServiceProvider());

        // Регистрируем потребителя
        module.AddConsumer<GenericMessage, WebAppMessageHandler>(c =>
        {
            c.QueueName = queueName;
            c.PrefetchCount = 1;
            // c.Durable = false;
            c.AutoDelete = true;
            // c.DeadLetter = new DeadLetterOptions
            // {
            //     Exchange = configuration["RabbitMQ:DeadLetterExchange"] ?? "dlx",
            //     RoutingKey = configuration["RabbitMQ:DeadLetterRoutingKey"] ?? "dead.letters",
            //     MaxRetries = configuration.GetValue("RabbitMQ:MaxRetryAttempts", 3)
            // };
        });

        // Регистрируем модуль в DI
        services.AddSingleton(module);
        services.AddSingleton(sp => sp.GetRequiredService<MessagingModule>().CreatePublisher());

        return services;
    }

    #endregion

}
