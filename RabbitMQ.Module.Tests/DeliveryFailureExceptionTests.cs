using Xunit;
using FluentAssertions;
using RabbitMQ.Module.DeliveryControl;

namespace RabbitMQ.Module.Tests;

public class DeliveryFailureExceptionTests
{
    [Fact]
    public void Constructor_ShouldSetMessage_WhenMessageIsProvided()
    {
        // Arrange
        string expectedMessage = "Delivery failed";

        // Act
        var exception = new DeliveryFailureException(expectedMessage);

        // Assert
        exception.Message.Should().Be(expectedMessage);
    }

    [Fact]
    public void Constructor_ShouldSetMessageAndInnerException_WhenBothAreProvided()
    {
        // Arrange
        string expectedMessage = "Delivery failed due to network error";
        var innerException = new Exception("Network timeout");

        // Act
        var exception = new DeliveryFailureException(expectedMessage, innerException);

        // Assert
        exception.Message.Should().Be(expectedMessage);
        exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    public void MessageId_ShouldBeSettableAndGettable()
    {
        // Arrange
        var exception = new DeliveryFailureException("Test message");
        string expectedMessageId = "msg-12345";

        // Act
        exception.MessageId = expectedMessageId;

        // Assert
        exception.MessageId.Should().Be(expectedMessageId);
    }

    [Fact]
    public void Reason_ShouldBeSettableAndGettable()
    {
        // Arrange
        var exception = new DeliveryFailureException("Test message");
        string expectedReason = "Queue full";

        // Act
        exception.Reason = expectedReason;

        // Assert
        exception.Reason.Should().Be(expectedReason);
    }

    [Fact]
    public void Properties_ShouldBeNullByDefault()
    {
        // Arrange
        var exception = new DeliveryFailureException("Test message");

        // Act & Assert
        exception.MessageId.Should().BeNull();
        exception.Reason.Should().BeNull();
    }

    [Fact]
    public void ShouldInheritFromException()
    {
        // Arrange
        var exception = new DeliveryFailureException("Test");

        // Act & Assert
        exception.Should().BeAssignableTo<Exception>();
    }
}