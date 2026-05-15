using Xunit;
using FluentAssertions;
using RabbitMQ.Module.Configuration;

namespace RabbitMQ.Module.Tests;

public class ConsumerOptionsTests
{
    #region QueueName Tests

    [Fact]
    public void QueueName_ShouldSetValidValue()
    {
        // Arrange
        var options = new ConsumerOptions();

        // Act
        options.QueueName = "test-queue";

        // Assert
        options.QueueName.Should().Be("test-queue");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void QueueName_ShouldThrowArgumentException_WhenValueIsNullOrWhitespace(string? invalidValue)
    {
        // Arrange
        var options = new ConsumerOptions();

        // Act
        Action act = () => options.QueueName = invalidValue;

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("QueueName не может быть пустым*");
    }

    [Fact]
    public void QueueName_ShouldThrowArgumentException_WhenValueTooLong()
    {
        // Arrange
        var options = new ConsumerOptions();
        var longName = new string('a', 256);

        // Act
        Action act = () => options.QueueName = longName;

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*слишком длинный (макс. 255 символов)*");
    }

    [Theory]
    [InlineData("queue@name")]
    [InlineData("queue name")]
    [InlineData("queue#123")]
    public void QueueName_ShouldThrowArgumentException_WhenContainsInvalidCharacters(string invalidName)
    {
        // Arrange
        var options = new ConsumerOptions();

        // Act
        Action act = () => options.QueueName = invalidName;

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*содержит недопустимые символы*");
    }

    [Theory]
    [InlineData("queue-name")]
    [InlineData("queue_name")]
    [InlineData("queue.name")]
    [InlineData("Queue123")]
    [InlineData("q")]
    public void QueueName_ShouldAcceptValidCharacters(string validName)
    {
        // Arrange
        var options = new ConsumerOptions();

        // Act
        options.QueueName = validName;

        // Assert
        options.QueueName.Should().Be(validName);
    }

    #endregion

    #region ExchangeName Tests

    [Fact]
    public void ExchangeName_ShouldSetValidValue()
    {
        // Arrange
        var options = new ConsumerOptions();

        // Act
        options.ExchangeName = "test-exchange";

        // Assert
        options.ExchangeName.Should().Be("test-exchange");
    }

    [Fact]
    public void ExchangeName_ShouldAcceptNull()
    {
        // Arrange
        var options = new ConsumerOptions();

        // Act
        options.ExchangeName = null;

        // Assert
        options.ExchangeName.Should().BeNull();
    }

    [Fact]
    public void ExchangeName_ShouldThrowArgumentException_WhenValueTooLong()
    {
        // Arrange
        var options = new ConsumerOptions();
        var longName = new string('a', 256);

        // Act
        Action act = () => options.ExchangeName = longName;

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*слишком длинный (макс. 255 символов)*");
    }

    #endregion

    #region RoutingKey Tests

    [Fact]
    public void RoutingKey_ShouldSetValidValue()
    {
        // Arrange
        var options = new ConsumerOptions();

        // Act
        options.RoutingKey = "test.routing.key";

        // Assert
        options.RoutingKey.Should().Be("test.routing.key");
    }

    [Fact]
    public void RoutingKey_ShouldAcceptNull()
    {
        // Arrange
        var options = new ConsumerOptions();

        // Act
        options.RoutingKey = null;

        // Assert
        options.RoutingKey.Should().BeNull();
    }

    [Fact]
    public void RoutingKey_ShouldThrowArgumentException_WhenValueTooLong()
    {
        // Arrange
        var options = new ConsumerOptions();
        var longKey = new string('a', 256);

        // Act
        Action act = () => options.RoutingKey = longKey;

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*слишком длинный (макс. 255 символов)*");
    }

    #endregion

    #region PrefetchCount Tests

    [Fact]
    public void PrefetchCount_ShouldHaveDefaultValue()
    {
        // Arrange & Act
        var options = new ConsumerOptions();

        // Assert
        options.PrefetchCount.Should().Be(1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1000)]
    public void PrefetchCount_ShouldSetValidValue(ushort validValue)
    {
        // Arrange
        var options = new ConsumerOptions();

        // Act
        options.PrefetchCount = validValue;

        // Assert
        options.PrefetchCount.Should().Be(validValue);
    }

    [Fact]
    public void PrefetchCount_ShouldThrowArgumentException_WhenValueIsZero()
    {
        // Arrange
        var options = new ConsumerOptions();

        // Act
        Action act = () => options.PrefetchCount = 0;

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*должен быть больше 0*");
    }

    [Fact]
    public void PrefetchCount_ShouldThrowArgumentException_WhenValueTooLarge()
    {
        // Arrange
        var options = new ConsumerOptions();

        // Act
        Action act = () => options.PrefetchCount = 1001;

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*не может превышать 1000*");
    }

    #endregion

    #region Durable Tests

    [Fact]
    public void Durable_ShouldHaveDefaultValueTrue()
    {
        // Arrange & Act
        var options = new ConsumerOptions();

        // Assert
        options.Durable.Should().BeTrue();
    }

    [Fact]
    public void Durable_ShouldSetValue()
    {
        // Arrange
        var options = new ConsumerOptions();

        // Act
        options.Durable = false;

        // Assert
        options.Durable.Should().BeFalse();
    }

    #endregion

    #region Exclusive Tests

    [Fact]
    public void Exclusive_ShouldHaveDefaultValueFalse()
    {
        // Arrange & Act
        var options = new ConsumerOptions();

        // Assert
        options.Exclusive.Should().BeFalse();
    }

    [Fact]
    public void Exclusive_ShouldSetValue()
    {
        // Arrange
        var options = new ConsumerOptions();

        // Act
        options.Exclusive = true;

        // Assert
        options.Exclusive.Should().BeTrue();
    }

    #endregion

    #region AutoDelete Tests

    [Fact]
    public void AutoDelete_ShouldHaveDefaultValueFalse()
    {
        // Arrange & Act
        var options = new ConsumerOptions();

        // Assert
        options.AutoDelete.Should().BeFalse();
    }

    [Fact]
    public void AutoDelete_ShouldSetValue()
    {
        // Arrange
        var options = new ConsumerOptions();

        // Act
        options.AutoDelete = true;

        // Assert
        options.AutoDelete.Should().BeTrue();
    }

    #endregion

    #region Arguments Tests

    [Fact]
    public void Arguments_ShouldBeNullByDefault()
    {
        // Arrange & Act
        var options = new ConsumerOptions();

        // Assert
        options.Arguments.Should().BeNull();
    }

    [Fact]
    public void Arguments_ShouldSetValue()
    {
        // Arrange
        var options = new ConsumerOptions();
        var args = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        options.Arguments = args;

        // Assert
        options.Arguments.Should().BeEquivalentTo(args);
    }

    #endregion

    #region AutoAck Tests

    [Fact]
    public void AutoAck_ShouldHaveDefaultValueFalse()
    {
        // Arrange & Act
        var options = new ConsumerOptions();

        // Assert
        options.AutoAck.Should().BeFalse();
    }

    [Fact]
    public void AutoAck_ShouldSetValue()
    {
        // Arrange
        var options = new ConsumerOptions();

        // Act
        options.AutoAck = true;

        // Assert
        options.AutoAck.Should().BeTrue();
    }

    #endregion

    #region MaxConcurrentCalls Tests

    [Fact]
    public void MaxConcurrentCalls_ShouldBeNullByDefault()
    {
        // Arrange & Act
        var options = new ConsumerOptions();

        // Assert
        options.MaxConcurrentCalls.Should().BeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void MaxConcurrentCalls_ShouldSetValidValue(int validValue)
    {
        // Arrange
        var options = new ConsumerOptions();

        // Act
        options.MaxConcurrentCalls = validValue;

        // Assert
        options.MaxConcurrentCalls.Should().Be(validValue);
    }

    [Fact]
    public void MaxConcurrentCalls_ShouldAcceptNull()
    {
        // Arrange
        var options = new ConsumerOptions();

        // Act
        options.MaxConcurrentCalls = null;

        // Assert
        options.MaxConcurrentCalls.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void MaxConcurrentCalls_ShouldThrowArgumentException_WhenValueInvalid(int invalidValue)
    {
        // Arrange
        var options = new ConsumerOptions();

        // Act
        Action act = () => options.MaxConcurrentCalls = invalidValue;

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*должен быть больше 0*");
    }

    #endregion

    #region Timeout Tests

    [Fact]
    public void Timeout_ShouldBeNullByDefault()
    {
        // Arrange & Act
        var options = new ConsumerOptions();

        // Assert
        options.Timeout.Should().BeNull();
    }

    [Fact]
    public void Timeout_ShouldSetValidValue()
    {
        // Arrange
        var options = new ConsumerOptions();
        var timeout = TimeSpan.FromSeconds(30);

        // Act
        options.Timeout = timeout;

        // Assert
        options.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Timeout_ShouldAcceptNull()
    {
        // Arrange
        var options = new ConsumerOptions();

        // Act
        options.Timeout = null;

        // Assert
        options.Timeout.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1000)]
    public void Timeout_ShouldThrowArgumentException_WhenValueInvalid(int milliseconds)
    {
        // Arrange
        var options = new ConsumerOptions();
        var invalidTimeout = TimeSpan.FromMilliseconds(milliseconds);

        // Act
        Action act = () => options.Timeout = invalidTimeout;

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*должен быть положительным*");
    }

    #endregion

    #region SingleActiveConsumer Tests

    [Fact]
    public void SingleActiveConsumer_ShouldHaveDefaultValueFalse()
    {
        // Arrange & Act
        var options = new ConsumerOptions();

        // Assert
        options.SingleActiveConsumer.Should().BeFalse();
    }

    [Fact]
    public void SingleActiveConsumer_ShouldSetValue()
    {
        // Arrange
        var options = new ConsumerOptions();

        // Act
        options.SingleActiveConsumer = true;

        // Assert
        options.SingleActiveConsumer.Should().BeTrue();
    }

    #endregion

    #region DeadLetter Tests

    [Fact]
    public void DeadLetter_ShouldBeNullByDefault()
    {
        // Arrange & Act
        var options = new ConsumerOptions();

        // Assert
        options.DeadLetter.Should().BeNull();
    }

    [Fact]
    public void DeadLetter_ShouldSetValue()
    {
        // Arrange
        var options = new ConsumerOptions();
        var deadLetter = new DeadLetterOptions();

        // Act
        options.DeadLetter = deadLetter;

        // Assert
        options.DeadLetter.Should().Be(deadLetter);
    }

    #endregion

    #region Ttl Tests

    [Fact]
    public void Ttl_ShouldBeNullByDefault()
    {
        // Arrange & Act
        var options = new ConsumerOptions();

        // Assert
        options.Ttl.Should().BeNull();
    }

    [Fact]
    public void Ttl_ShouldSetValue()
    {
        // Arrange
        var options = new ConsumerOptions();
        var ttl = new QueueTtlOptions();

        // Act
        options.Ttl = ttl;

        // Assert
        options.Ttl.Should().Be(ttl);
    }

    #endregion

    #region Length Tests

    [Fact]
    public void Length_ShouldBeNullByDefault()
    {
        // Arrange & Act
        var options = new ConsumerOptions();

        // Assert
        options.Length.Should().BeNull();
    }

    [Fact]
    public void Length_ShouldSetValue()
    {
        // Arrange
        var options = new ConsumerOptions();
        var length = new QueueLengthOptions();

        // Act
        options.Length = length;

        // Assert
        options.Length.Should().Be(length);
    }

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_ShouldPass_WhenAllPropertiesValid()
    {
        // Arrange
        var options = new ConsumerOptions
        {
            QueueName = "test-queue",
            ExchangeName = "test-exchange",
            RoutingKey = "test.key",
            PrefetchCount = 10,
            Durable = true,
            Exclusive = false,
            AutoDelete = false,
            Arguments = new Dictionary<string, object> { ["key"] = "value" },
            AutoAck = false,
            MaxConcurrentCalls = 5,
            Timeout = TimeSpan.FromSeconds(30),
            SingleActiveConsumer = false,
            DeadLetter = new DeadLetterOptions { Exchange = "dlx" },
            Ttl = new QueueTtlOptions { MessageTtl = 300000 }, // 5 minutes in milliseconds
            Length = new QueueLengthOptions { MaxLength = 1000 }
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_ShouldThrowInvalidOperationException_WhenQueueNameEmpty()
    {
        // Arrange
        var options = new ConsumerOptions();

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*QueueName не может быть пустым*");
    }

    [Fact]
    public void Validate_ShouldThrowInvalidOperationException_WhenArgumentsContainEmptyKey()
    {
        // Arrange
        var options = new ConsumerOptions
        {
            QueueName = "test-queue",
            Arguments = new Dictionary<string, object> { [""] = "value" }
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Ключ аргумента не может быть пустым*");
    }

    [Fact]
    public void Validate_ShouldCallValidateOnNestedOptions()
    {
        // Arrange
        var options = new ConsumerOptions
        {
            QueueName = "test-queue",
            DeadLetter = new DeadLetterOptions { Exchange = "dlx" },
            Ttl = new QueueTtlOptions { MessageTtl = 30000 },
            Length = new QueueLengthOptions { MaxLength = 1000 }
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }
    #endregion
}