namespace RabbitMQ.Module.Extensions;

using Configuration;

using Contracts;

using Messaging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

        // Регистрируем ConsumerHostedService
        services.AddHostedService(serviceProvider =>
        {
            var module = serviceProvider.GetRequiredService<MessagingModule>();

            var dispatcher = new MessageDispatcher(
                module.Registry,
                module.Serializer,
                module.LoggerFactory.CreateLogger<MessageDispatcher>(),
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
