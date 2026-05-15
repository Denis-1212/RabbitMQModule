using Xunit;
using FluentAssertions;
using RabbitMQ.Module.Registration;
using RabbitMQ.Module.Configuration;
using RabbitMQ.Module.Contracts;

namespace RabbitMQ.Module.Tests;

internal class TestMessage
{
    public string Content { get; set; }
}

internal class TestHandler : IMessageHandler<string>
{
    public Task HandleAsync(string message, IMessageContext context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

internal class TestHandlerInt : IMessageHandler<int>
{
    public Task HandleAsync(int message, IMessageContext context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

internal class TestHandlerMessage : IMessageHandler<TestMessage>
{
    public Task HandleAsync(TestMessage message, IMessageContext context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public class ConsumerRegistrationCollectionTests
{
    [Fact]
    public void Count_ShouldReturnZero_WhenCollectionIsEmpty()
    {
        // Arrange
        var collection = new ConsumerRegistrationCollection();

        // Act & Assert
        collection.Count.Should().Be(0);
    }

    [Fact]
    public void Add_ShouldThrowArgumentNullException_WhenRegistrationIsNull()
    {
        // Arrange
        var collection = new ConsumerRegistrationCollection();

        // Act & Assert
        collection.Invoking(c => c.Add(null!))
            .Should().Throw<ArgumentNullException>()
            .WithParameterName("registration");
    }

    [Fact]
    public void Add_ShouldAddRegistration_WhenValidRegistrationIsProvided()
    {
        // Arrange
        var collection = new ConsumerRegistrationCollection();
        var options = new ConsumerOptions { QueueName = "test-queue" };
        var registration = new ConsumerRegistration(typeof(string), typeof(TestHandler), options);

        // Act
        collection.Add(registration);

        // Assert
        collection.Count.Should().Be(1);
        collection.Get(registration.RegistrationId).Should().Be(registration);
    }

    [Fact]
    public void Add_ShouldThrowInvalidOperationException_WhenRegistrationIdAlreadyExists()
    {
        // Arrange
        var collection = new ConsumerRegistrationCollection();
        var options = new ConsumerOptions { QueueName = "test-queue" };
        var registration = new ConsumerRegistration(typeof(string), typeof(TestHandler), options);

        collection.Add(registration);

        // Act & Assert - Adding the same registration twice should fail
        collection.Invoking(c => c.Add(registration))
            .Should().Throw<InvalidOperationException>()
            .WithMessage($"Регистрация с ID {registration.RegistrationId} уже существует");
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenRegistrationIdDoesNotExist()
    {
        // Arrange
        var collection = new ConsumerRegistrationCollection();

        // Act
        bool result = collection.Remove("non-existent-id");

        // Assert
        result.Should().BeFalse();
        collection.Count.Should().Be(0);
    }

    [Fact]
    public void Remove_ShouldReturnTrueAndRemoveRegistration_WhenRegistrationIdExists()
    {
        // Arrange
        var collection = new ConsumerRegistrationCollection();
        var options = new ConsumerOptions { QueueName = "test-queue" };
        var registration = new ConsumerRegistration(typeof(string), typeof(TestHandler), options);
        collection.Add(registration);

        // Act
        bool result = collection.Remove(registration.RegistrationId);

        // Assert
        result.Should().BeTrue();
        collection.Count.Should().Be(0);
        collection.Get(registration.RegistrationId).Should().BeNull();
    }

    [Fact]
    public void Get_ShouldReturnNull_WhenRegistrationIdDoesNotExist()
    {
        // Arrange
        var collection = new ConsumerRegistrationCollection();

        // Act
        var result = collection.Get("non-existent-id");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Get_ShouldReturnRegistration_WhenRegistrationIdExists()
    {
        // Arrange
        var collection = new ConsumerRegistrationCollection();
        var options = new ConsumerOptions { QueueName = "test-queue" };
        var registration = new ConsumerRegistration(typeof(string), typeof(TestHandler), options);
        collection.Add(registration);

        // Act
        var result = collection.Get(registration.RegistrationId);

        // Assert
        result.Should().Be(registration);
    }

    [Fact]
    public void GetForMessageType_ShouldReturnEmptyCollection_WhenNoRegistrationsForType()
    {
        // Arrange
        var collection = new ConsumerRegistrationCollection();
        var options = new ConsumerOptions { QueueName = "test-queue" };
        var registration = new ConsumerRegistration(typeof(string), typeof(TestHandler), options);
        collection.Add(registration);

        // Act
        var result = collection.GetForMessageType(typeof(int));

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetForMessageType_ShouldReturnRegistrations_WhenRegistrationsExistForType()
    {
        // Arrange
        var collection = new ConsumerRegistrationCollection();
        var options1 = new ConsumerOptions { QueueName = "test-queue1" };
        var registration1 = new ConsumerRegistration(typeof(string), typeof(TestHandler), options1);
        var options2 = new ConsumerOptions { QueueName = "test-queue2" };
        var registration2 = new ConsumerRegistration(typeof(string), typeof(TestHandler), options2);
        var options3 = new ConsumerOptions { QueueName = "test-queue3" };
        var registration3 = new ConsumerRegistration(typeof(TestMessage), typeof(TestHandlerMessage), options3);

        collection.Add(registration1);
        collection.Add(registration2);
        collection.Add(registration3);

        // Act
        var result = collection.GetForMessageType(typeof(string)).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(registration1);
        result.Should().Contain(registration2);
    }

    [Fact]
    public void GetAll_ShouldReturnAllRegistrations()
    {
        // Arrange
        var collection = new ConsumerRegistrationCollection();
        var options1 = new ConsumerOptions { QueueName = "test-queue1" };
        var registration1 = new ConsumerRegistration(typeof(string), typeof(TestHandler), options1);
        var options2 = new ConsumerOptions { QueueName = "test-queue2" };
        var registration2 = new ConsumerRegistration(typeof(TestMessage), typeof(TestHandlerMessage), options2);

        collection.Add(registration1);
        collection.Add(registration2);

        // Act
        var result = collection.GetAll().ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(registration1);
        result.Should().Contain(registration2);
    }

    [Fact]
    public void GetEnumerator_ShouldEnumerateAllRegistrations()
    {
        // Arrange
        var collection = new ConsumerRegistrationCollection();
        var options1 = new ConsumerOptions { QueueName = "test-queue1" };
        var registration1 = new ConsumerRegistration(typeof(string), typeof(TestHandler), options1);
        var options2 = new ConsumerOptions { QueueName = "test-queue2" };
        var registration2 = new ConsumerRegistration(typeof(TestMessage), typeof(TestHandlerMessage), options2);

        collection.Add(registration1);
        collection.Add(registration2);

        // Act
        var result = collection.ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(registration1);
        result.Should().Contain(registration2);
    }

    [Fact]
    public async Task Collection_ShouldHandleConcurrentAccess()
    {
        // Arrange
        var collection = new ConsumerRegistrationCollection();
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 10; i++)
        {
            int index = i;
            tasks.Add(Task.Run(() =>
            {
                var options = new ConsumerOptions { QueueName = $"test-queue{index}" };
                var registration = new ConsumerRegistration(typeof(string), typeof(TestHandler), options);
                collection.Add(registration);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        collection.Count.Should().Be(10);
    }
}