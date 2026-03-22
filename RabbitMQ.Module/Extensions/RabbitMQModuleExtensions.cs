namespace RabbitMQ.Module.Extensions;

using Configuration;

using Contracts;

using Deduplication;

using DeliveryControl;

using Messaging;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using StackExchange.Redis;

/// <summary>
/// Методы расширения для интеграции RabbitMQ модуля в ASP.NET Core
/// </summary>
public static class RabbitMQModuleExtensions
{

    #region Methods

    /// <summary>
    /// Добавляет RabbitMQ модуль в контейнер DI
    /// </summary>
    /// <param name = "services">Коллекция сервисов</param>
    /// <param name = "configure">Действие для настройки параметров</param>
    /// <param name = "configureModule">Действие для дополнительной настройки модуля (регистрация потребителей)</param>
    /// <returns>Коллекция сервисов для Fluent API</returns>
    public static IServiceCollection AddRabbitMQModule(
        this IServiceCollection services,
        Action<MessagingOptions> configure,
        Action<MessagingModule>? configureModule = null)
    {
        // Регистрируем модуль как синглтон
        services.AddSingleton(serviceProvider =>
        {
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();

            var options = new MessagingOptions();
            configure(options);

            // Если настроен Redis и не передан явно, регистрируем ConnectionMultiplexer
            if (options.Deduplication.StoreType == DeduplicationStoreType.Redis)
            {
                string? connectionString = options.Deduplication.RedisConnectionString
                                           ?? configuration.GetConnectionString("Redis");

                if (!string.IsNullOrEmpty(connectionString))
                {
                    ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(connectionString);
                    services.AddSingleton<IConnectionMultiplexer>(redis);
                }
            }

            var module = MessagingModule.Create(
                configure,
                loggerFactory,
                serviceProvider);

            configureModule?.Invoke(module);

            return module;
        });

        // Регистрируем Publisher как Scoped
        services.AddScoped<IPublisher>(serviceProvider =>
        {
            var module = serviceProvider.GetRequiredService<MessagingModule>();
            return module.CreatePublisher();
        });

        // Регистрируем метрики
        services.AddSingleton<IDeliveryMetrics>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<DefaultDeliveryMetrics>>();
            return new DefaultDeliveryMetrics(logger);
        });

        // Регистрируем ConsumerHostedService
        services.AddHostedService(serviceProvider =>
        {
            var module = serviceProvider.GetRequiredService<MessagingModule>();
            var metrics = serviceProvider.GetRequiredService<IDeliveryMetrics>();
            var publisher = serviceProvider.GetRequiredService<IPublisher>();

            var dispatcher = new MessageDispatcher(
                module.Registry,
                module.Serializer,
                module.LoggerFactory.CreateLogger<MessageDispatcher>(),
                module.Options,
                metrics,
                module.ServiceProvider);

            return new ConsumerHostedService(
                module.ConnectionManager,
                module.Registry,
                dispatcher,
                module.Options,
                module.LoggerFactory.CreateLogger<ConsumerHostedService>());
        });

        return services;
    }

    /// <summary>
    /// Получает издателя сообщений из контейнера (для удобства)
    /// </summary>
    public static IPublisher GetPublisher(this IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<IPublisher>();
    }

    #endregion

}
