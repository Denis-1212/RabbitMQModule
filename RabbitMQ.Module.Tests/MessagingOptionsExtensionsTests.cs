using Xunit;
using FluentAssertions;
using RabbitMQ.Module.Configuration;
using Testcontainers.RabbitMq;
using Xunit.Abstractions;

namespace RabbitMQ.Module.Tests;

public class MessagingOptionsExtensionsTests : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder("rabbitmq:3.12-management-alpine")
        .WithPortBinding(5672, true)
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    public async Task InitializeAsync()
    {
        await _rabbitMqContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _rabbitMqContainer.DisposeAsync();
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldReturnTrue_WhenConnectionSucceeds()
    {
        // Arrange
        ushort port = _rabbitMqContainer.GetMappedPublicPort(5672);
        var options = new MessagingOptions
        {
            ConnectionString = $"amqp://guest:guest@localhost:{port}"
        };

        // Act
        bool result = await options.TestConnectionAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldReturnFalse_WhenConnectionFails()
    {
        // Arrange
        var options = new MessagingOptions
        {
            ConnectionString = "amqp://guest:guest@invalidhost:5672"
        };

        // Act
        bool result = await options.TestConnectionAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void BuildConnectionString_ShouldReturnDefaultConnectionString()
    {
        // Act
        string result = MessagingOptionsExtensions.BuildConnectionString();

        // Assert
        result.Should().Be("amqp://guest:guest@localhost:5672/");
    }

    [Fact]
    public void BuildConnectionString_ShouldReturnConnectionStringWithCustomHost()
    {
        // Act
        string result = MessagingOptionsExtensions.BuildConnectionString(host: "rabbitmq.example.com");

        // Assert
        result.Should().Be("amqp://guest:guest@rabbitmq.example.com:5672/");
    }

    [Fact]
    public void BuildConnectionString_ShouldReturnConnectionStringWithCustomPort()
    {
        // Act
        string result = MessagingOptionsExtensions.BuildConnectionString(port: 5673);

        // Assert
        result.Should().Be("amqp://guest:guest@localhost:5673/");
    }

    [Fact]
    public void BuildConnectionString_ShouldReturnConnectionStringWithCustomVHost()
    {
        // Act
        string result = MessagingOptionsExtensions.BuildConnectionString(vHost: "myvhost");

        // Assert
        result.Should().Be("amqp://guest:guest@localhost:5672/myvhost");
    }

    [Fact]
    public void BuildConnectionString_ShouldReturnConnectionStringWithCustomUserNameAndPassword()
    {
        // Act
        string result = MessagingOptionsExtensions.BuildConnectionString(userName: "admin", password: "secret");

        // Assert
        result.Should().Be("amqp://admin:secret@localhost:5672/");
    }

    [Fact]
    public void BuildConnectionString_ShouldReturnSslConnectionString_WhenUseSslIsTrue()
    {
        // Act
        string result = MessagingOptionsExtensions.BuildConnectionString(useSsl: true);

        // Assert
        result.Should().Be("amqps://guest:guest@localhost:5672/");
    }

    [Fact]
    public void BuildConnectionString_ShouldReturnConnectionStringWithAllCustomParameters()
    {
        // Act
        string result = MessagingOptionsExtensions.BuildConnectionString(
            host: "secure.rabbitmq.com",
            port: 5671,
            vHost: "production",
            userName: "producer",
            password: "prodpass",
            useSsl: true);

        // Assert
        result.Should().Be("amqps://producer:prodpass@secure.rabbitmq.com:5671/production");
    }

    [Fact]
    public void BuildConnectionString_ShouldHandleRootVHost()
    {
        // Act
        string result = MessagingOptionsExtensions.BuildConnectionString(vHost: "/");

        // Assert
        result.Should().Be("amqp://guest:guest@localhost:5672/");
    }

    [Fact]
    public void BuildConnectionString_ShouldTrimLeadingSlashFromVHost()
    {
        // Act
        string result = MessagingOptionsExtensions.BuildConnectionString(vHost: "/myvhost");

        // Assert
        result.Should().Be("amqp://guest:guest@localhost:5672/myvhost");
    }
}