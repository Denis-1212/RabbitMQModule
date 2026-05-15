using Xunit;
using FluentAssertions;
using RabbitMQ.Module.Configuration;

namespace RabbitMQ.Module.Tests;

public class QueueTtlOptionsTests
{
    [Fact]
    public void Validate_ShouldNotThrow_WhenAllPropertiesAreValid()
    {
        // Arrange
        var options = new QueueTtlOptions
        {
            MessageTtl = 60000,
            QueueTtl = 3600000
        };

        // Act & Assert
        options.Invoking(o => o.Validate()).Should().NotThrow();
    }

    [Fact]
    public void Validate_ShouldNotThrow_WhenPropertiesAreNull()
    {
        // Arrange
        var options = new QueueTtlOptions();

        // Act & Assert
        options.Invoking(o => o.Validate()).Should().NotThrow();
    }

    [Fact]
    public void Validate_ShouldThrowInvalidOperationException_WhenMessageTtlIsZero()
    {
        // Arrange
        var options = new QueueTtlOptions { MessageTtl = 0 };

        // Act & Assert
        options.Invoking(o => o.Validate())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("MessageTtl должен быть больше 0: 0");
    }

    [Fact]
    public void Validate_ShouldThrowInvalidOperationException_WhenMessageTtlIsNegative()
    {
        // Arrange
        var options = new QueueTtlOptions { MessageTtl = -1000 };

        // Act & Assert
        options.Invoking(o => o.Validate())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("MessageTtl должен быть больше 0: -1000");
    }

    [Fact]
    public void Validate_ShouldThrowInvalidOperationException_WhenQueueTtlIsZero()
    {
        // Arrange
        var options = new QueueTtlOptions { QueueTtl = 0 };

        // Act & Assert
        options.Invoking(o => o.Validate())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("QueueTtl должен быть больше 0: 0");
    }

    [Fact]
    public void Validate_ShouldThrowInvalidOperationException_WhenQueueTtlIsNegative()
    {
        // Arrange
        var options = new QueueTtlOptions { QueueTtl = -5000 };

        // Act & Assert
        options.Invoking(o => o.Validate())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("QueueTtl должен быть больше 0: -5000");
    }

    [Fact]
    public void ApplyTo_ShouldAddMessageTtlToArgs_WhenMessageTtlIsSet()
    {
        // Arrange
        var options = new QueueTtlOptions { MessageTtl = 30000 };
        var args = new Dictionary<string, object>();

        // Act
        options.ApplyTo(args);

        // Assert
        args.Should().ContainKey("x-message-ttl");
        args["x-message-ttl"].Should().Be(30000);
    }

    [Fact]
    public void ApplyTo_ShouldAddQueueTtlToArgs_WhenQueueTtlIsSet()
    {
        // Arrange
        var options = new QueueTtlOptions { QueueTtl = 7200000 };
        var args = new Dictionary<string, object>();

        // Act
        options.ApplyTo(args);

        // Assert
        args.Should().ContainKey("x-expires");
        args["x-expires"].Should().Be(7200000);
    }

    [Fact]
    public void ApplyTo_ShouldNotAddPropertiesToArgs_WhenPropertiesAreNull()
    {
        // Arrange
        var options = new QueueTtlOptions();
        var args = new Dictionary<string, object>();

        // Act
        options.ApplyTo(args);

        // Assert
        args.Should().BeEmpty();
    }

    [Fact]
    public void ApplyTo_ShouldAddAllPropertiesToArgs_WhenAllPropertiesAreSet()
    {
        // Arrange
        var options = new QueueTtlOptions
        {
            MessageTtl = 45000,
            QueueTtl = 1800000
        };
        var args = new Dictionary<string, object>();

        // Act
        options.ApplyTo(args);

        // Assert
        args.Should().HaveCount(2);
        args["x-message-ttl"].Should().Be(45000);
        args["x-expires"].Should().Be(1800000);
    }

    [Fact]
    public void Clone_ShouldReturnNewInstanceWithSameValues()
    {
        // Arrange
        var original = new QueueTtlOptions
        {
            MessageTtl = 120000,
            QueueTtl = 86400000
        };

        // Act
        var clone = original.Clone();

        // Assert
        clone.Should().NotBeSameAs(original);
        clone.MessageTtl.Should().Be(120000);
        clone.QueueTtl.Should().Be(86400000);
    }

    [Fact]
    public void Clone_ShouldReturnNewInstanceWithNullValues_WhenOriginalHasNulls()
    {
        // Arrange
        var original = new QueueTtlOptions();

        // Act
        var clone = original.Clone();

        // Assert
        clone.Should().NotBeSameAs(original);
        clone.MessageTtl.Should().BeNull();
        clone.QueueTtl.Should().BeNull();
    }
}