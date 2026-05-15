using Xunit;
using FluentAssertions;
using RabbitMQ.Module.Configuration;

namespace RabbitMQ.Module.Tests;

public class QueueLengthOptionsTests
{
    [Fact]
    public void Validate_ShouldNotThrow_WhenAllPropertiesAreValid()
    {
        // Arrange
        var options = new QueueLengthOptions
        {
            MaxLength = 100,
            MaxLengthBytes = 1024,
            OverflowStrategy = "drop-head"
        };

        // Act & Assert
        options.Invoking(o => o.Validate()).Should().NotThrow();
    }

    [Fact]
    public void Validate_ShouldNotThrow_WhenPropertiesAreNull()
    {
        // Arrange
        var options = new QueueLengthOptions();

        // Act & Assert
        options.Invoking(o => o.Validate()).Should().NotThrow();
    }

    [Fact]
    public void Validate_ShouldThrowInvalidOperationException_WhenMaxLengthIsZero()
    {
        // Arrange
        var options = new QueueLengthOptions { MaxLength = 0 };

        // Act & Assert
        options.Invoking(o => o.Validate())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("MaxLength должен быть больше 0: 0");
    }

    [Fact]
    public void Validate_ShouldThrowInvalidOperationException_WhenMaxLengthIsNegative()
    {
        // Arrange
        var options = new QueueLengthOptions { MaxLength = -1 };

        // Act & Assert
        options.Invoking(o => o.Validate())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("MaxLength должен быть больше 0: -1");
    }

    [Fact]
    public void Validate_ShouldThrowInvalidOperationException_WhenMaxLengthBytesIsZero()
    {
        // Arrange
        var options = new QueueLengthOptions { MaxLengthBytes = 0 };

        // Act & Assert
        options.Invoking(o => o.Validate())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("MaxLengthBytes должен быть больше 0: 0");
    }

    [Fact]
    public void Validate_ShouldThrowInvalidOperationException_WhenMaxLengthBytesIsNegative()
    {
        // Arrange
        var options = new QueueLengthOptions { MaxLengthBytes = -1024 };

        // Act & Assert
        options.Invoking(o => o.Validate())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("MaxLengthBytes должен быть больше 0: -1024");
    }

    [Fact]
    public void Validate_ShouldThrowInvalidOperationException_WhenOverflowStrategyIsInvalid()
    {
        // Arrange
        var options = new QueueLengthOptions { OverflowStrategy = "invalid-strategy" };

        // Act & Assert
        options.Invoking(o => o.Validate())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("OverflowStrategy должен быть 'drop-head' или 'reject-publish': invalid-strategy");
    }

    [Fact]
    public void Validate_ShouldNotThrow_WhenOverflowStrategyIsDropHead()
    {
        // Arrange
        var options = new QueueLengthOptions { OverflowStrategy = "drop-head" };

        // Act & Assert
        options.Invoking(o => o.Validate()).Should().NotThrow();
    }

    [Fact]
    public void Validate_ShouldNotThrow_WhenOverflowStrategyIsRejectPublish()
    {
        // Arrange
        var options = new QueueLengthOptions { OverflowStrategy = "reject-publish" };

        // Act & Assert
        options.Invoking(o => o.Validate()).Should().NotThrow();
    }

    [Fact]
    public void ApplyTo_ShouldAddMaxLengthToArgs_WhenMaxLengthIsSet()
    {
        // Arrange
        var options = new QueueLengthOptions { MaxLength = 50 };
        var args = new Dictionary<string, object>();

        // Act
        options.ApplyTo(args);

        // Assert
        args.Should().ContainKey("x-max-length");
        args["x-max-length"].Should().Be(50);
    }

    [Fact]
    public void ApplyTo_ShouldAddMaxLengthBytesToArgs_WhenMaxLengthBytesIsSet()
    {
        // Arrange
        var options = new QueueLengthOptions { MaxLengthBytes = 2048 };
        var args = new Dictionary<string, object>();

        // Act
        options.ApplyTo(args);

        // Assert
        args.Should().ContainKey("x-max-length-bytes");
        args["x-max-length-bytes"].Should().Be(2048);
    }

    [Fact]
    public void ApplyTo_ShouldAddOverflowStrategyToArgs_WhenOverflowStrategyIsSet()
    {
        // Arrange
        var options = new QueueLengthOptions { OverflowStrategy = "reject-publish" };
        var args = new Dictionary<string, object>();

        // Act
        options.ApplyTo(args);

        // Assert
        args.Should().ContainKey("x-overflow");
        args["x-overflow"].Should().Be("reject-publish");
    }

    [Fact]
    public void ApplyTo_ShouldNotAddPropertiesToArgs_WhenPropertiesAreNull()
    {
        // Arrange
        var options = new QueueLengthOptions();
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
        var options = new QueueLengthOptions
        {
            MaxLength = 100,
            MaxLengthBytes = 10240,
            OverflowStrategy = "drop-head"
        };
        var args = new Dictionary<string, object>();

        // Act
        options.ApplyTo(args);

        // Assert
        args.Should().HaveCount(3);
        args["x-max-length"].Should().Be(100);
        args["x-max-length-bytes"].Should().Be(10240);
        args["x-overflow"].Should().Be("drop-head");
    }

    [Fact]
    public void Clone_ShouldReturnNewInstanceWithSameValues()
    {
        // Arrange
        var original = new QueueLengthOptions
        {
            MaxLength = 200,
            MaxLengthBytes = 5120,
            OverflowStrategy = "reject-publish"
        };

        // Act
        var clone = original.Clone();

        // Assert
        clone.Should().NotBeSameAs(original);
        clone.MaxLength.Should().Be(200);
        clone.MaxLengthBytes.Should().Be(5120);
        clone.OverflowStrategy.Should().Be("reject-publish");
    }

    [Fact]
    public void Clone_ShouldReturnNewInstanceWithNullValues_WhenOriginalHasNulls()
    {
        // Arrange
        var original = new QueueLengthOptions();

        // Act
        var clone = original.Clone();

        // Assert
        clone.Should().NotBeSameAs(original);
        clone.MaxLength.Should().BeNull();
        clone.MaxLengthBytes.Should().BeNull();
        clone.OverflowStrategy.Should().BeNull();
    }
}