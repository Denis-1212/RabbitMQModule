using FluentAssertions;
using Moq;
using RabbitMQ.Module.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Module.Contracts;

namespace RabbitMQ.Module.Tests;

public class RabbitMQModuleExtensionsTests
{
    [Fact]
    public void AddRabbitMQModule_ShouldAddServicesToServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new Mock<IConfiguration>();
        var loggerFactory = new Mock<ILoggerFactory>();

        services.AddSingleton(configuration.Object);
        services.AddSingleton(loggerFactory.Object);
        services.AddLogging(); // Add logging services

        // Act
        services.AddRabbitMQModule(options => { /* configure options */ });

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        // Check that MessagingModule is registered
        var module = serviceProvider.GetService<MessagingModule>();
        module.Should().NotBeNull();

        // Check that IPublisher is registered as scoped
        var publisher = serviceProvider.GetService<IPublisher>();
        publisher.Should().NotBeNull();

        // Check that IDeliveryMetrics is registered
        var metrics = serviceProvider.GetService<IDeliveryMetrics>();
        metrics.Should().NotBeNull();
    }

    [Fact]
    public void AddRabbitMQModule_ShouldCallConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new Mock<IConfiguration>();
        var loggerFactory = new Mock<ILoggerFactory>();
        bool configureCalled = false;

        services.AddSingleton(configuration.Object);
        services.AddSingleton(loggerFactory.Object);
        services.AddLogging();

        // Act
        services.AddRabbitMQModule(options => { configureCalled = true; });

        // Force resolution to trigger the factory
        var serviceProvider = services.BuildServiceProvider();
        var module = serviceProvider.GetService<MessagingModule>();

        // Assert
        configureCalled.Should().BeTrue();
    }

    [Fact]
    public void AddRabbitMQModule_ShouldCallConfigureModuleAction_WhenProvided()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new Mock<IConfiguration>();
        var loggerFactory = new Mock<ILoggerFactory>();
        bool configureModuleCalled = false;

        services.AddSingleton(configuration.Object);
        services.AddSingleton(loggerFactory.Object);
        services.AddLogging();

        // Act
        services.AddRabbitMQModule(
            options => { /* configure options */ },
            module => { configureModuleCalled = true; });

        // Force resolution
        var serviceProvider = services.BuildServiceProvider();
        var module = serviceProvider.GetService<MessagingModule>();

        // Assert
        configureModuleCalled.Should().BeTrue();
    }

    [Fact]
    public void GetPublisher_ShouldReturnPublisherFromServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new Mock<IConfiguration>();
        var loggerFactory = new Mock<ILoggerFactory>();
        var mockPublisher = new Mock<IPublisher>();

        services.AddSingleton(configuration.Object);
        services.AddSingleton(loggerFactory.Object);
        services.AddLogging();
        services.AddRabbitMQModule(options => { /* configure options */ });
        services.AddScoped(_ => mockPublisher.Object); // Override the publisher

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var publisher = serviceProvider.GetPublisher();

        // Assert
        publisher.Should().Be(mockPublisher.Object);
    }

    [Fact]
    public void AddRabbitMQModule_ShouldRegisterConsumerHostedService()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new Mock<IConfiguration>();
        var loggerFactory = new Mock<ILoggerFactory>();

        services.AddSingleton(configuration.Object);
        services.AddSingleton(loggerFactory.Object);
        services.AddLogging();

        // Act
        services.AddRabbitMQModule(options => { /* configure options */ });

        // Assert
        var hostedServices = services.Where(sd => sd.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService));
        hostedServices.Should().HaveCount(1);
    }
}