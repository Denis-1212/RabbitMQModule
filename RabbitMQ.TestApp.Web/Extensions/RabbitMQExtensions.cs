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
        // Регистрируем модуль через фабрику, которая получит реальный ServiceProvider
        services.AddSingleton(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            string queueName = configuration["RabbitMQ:QueueName"] ?? "webapp.messages";

            var module = MessagingModule.Create(
                options =>
                {
                    options.ConnectionString = configuration["RabbitMQ:ConnectionString"]
                                               ?? "amqp://guest:guest@localhost:5672/";

                    options.ClientProvidedName = configuration["RabbitMQ:ClientProvidedName"] ?? "WebApp";
                    options.DeliveryControl.PublisherConfirmsEnabled =
                        configuration.GetValue("RabbitMQ:PublisherConfirmsEnabled", true);

                    options.DeliveryControl.EnableDeadLetter =
                        configuration.GetValue("RabbitMQ:EnableDeadLetter", true);

                    options.DeliveryControl.MaxRetryAttempts =
                        configuration.GetValue("RabbitMQ:MaxRetryAttempts", 3);
                },
                loggerFactory,
                sp);

            // Регистрируем потребителя
            module.AddConsumer<GenericMessage, WebAppMessageHandler>(c =>
            {
                c.QueueName = queueName;
                c.PrefetchCount = 1;
                c.Durable = true;
                c.AutoDelete = false;
            });

            return module;
        });

        // Регистрируем Publisher
        services.AddSingleton(sp => sp.GetRequiredService<MessagingModule>().CreatePublisher());

        return services;
    }

    #endregion

}
