using Xunit;
using FluentAssertions;
using RabbitMQ.Module.Configuration;
using RabbitMQ.Client;

namespace RabbitMQ.Module.Tests;

public class PublishConfigurationTests
{
    #region Constructor and Default Values Tests

    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var config = new PublishConfiguration();

        // Assert
        config.Exchange.Should().BeEmpty();
        config.RoutingKey.Should().BeEmpty();
        config.MessageId.Should().BeNull();
        config.Mandatory.Should().BeFalse();
        config.UsePublisherConfirms.Should().BeTrue();
        config.Expiration.Should().BeNull();
        config.Priority.Should().Be((byte)0);
        config.CorrelationId.Should().BeNull();
        config.ReplyTo.Should().BeNull();
        config.Headers.Should().BeEmpty();
        config.CustomProperties.Should().BeEmpty();
    }

    #endregion

    #region WithExchange Tests

    [Fact]
    public void WithExchange_ShouldSetExchange()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        config.WithExchange("test-exchange");

        // Assert
        config.Exchange.Should().Be("test-exchange");
    }

    [Fact]
    public void WithExchange_ShouldThrowArgumentNullException_WhenExchangeIsNull()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        Action act = () => config.WithExchange(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("exchange");
    }

    #endregion

    #region WithRoutingKey Tests

    [Fact]
    public void WithRoutingKey_ShouldSetRoutingKey()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        config.WithRoutingKey("test.routing.key");

        // Assert
        config.RoutingKey.Should().Be("test.routing.key");
    }

    [Fact]
    public void WithRoutingKey_ShouldThrowArgumentNullException_WhenRoutingKeyIsNull()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        Action act = () => config.WithRoutingKey(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("routingKey");
    }

    #endregion

    #region WithMessageId Tests

    [Fact]
    public void WithMessageId_ShouldSetMessageId()
    {
        // Arrange
        var config = new PublishConfiguration();
        var messageId = "msg-123";

        // Act
        config.WithMessageId(messageId);

        // Assert
        config.MessageId.Should().Be(messageId);
    }

    [Fact]
    public void WithMessageId_ShouldThrowArgumentNullException_WhenMessageIdIsNull()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        Action act = () => config.WithMessageId(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("messageId");
    }

    #endregion

    #region WithMandatory Tests

    [Fact]
    public void WithMandatory_ShouldSetMandatoryToTrue_WhenCalledWithTrue()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        config.WithMandatory(true);

        // Assert
        config.Mandatory.Should().BeTrue();
    }

    [Fact]
    public void WithMandatory_ShouldSetMandatoryToFalse_WhenCalledWithFalse()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        config.WithMandatory(false);

        // Assert
        config.Mandatory.Should().BeFalse();
    }

    [Fact]
    public void WithMandatory_ShouldSetMandatoryToTrue_WhenCalledWithoutParameters()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        config.WithMandatory();

        // Assert
        config.Mandatory.Should().BeTrue();
    }

    #endregion

    #region WithPublisherConfirms Tests

    [Fact]
    public void WithPublisherConfirms_ShouldSetUsePublisherConfirmsToTrue_WhenCalledWithTrue()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        config.WithPublisherConfirms(true);

        // Assert
        config.UsePublisherConfirms.Should().BeTrue();
    }

    [Fact]
    public void WithPublisherConfirms_ShouldSetUsePublisherConfirmsToFalse_WhenCalledWithFalse()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        config.WithPublisherConfirms(false);

        // Assert
        config.UsePublisherConfirms.Should().BeFalse();
    }

    [Fact]
    public void WithPublisherConfirms_ShouldSetUsePublisherConfirmsToTrue_WhenCalledWithoutParameters()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        config.WithPublisherConfirms();

        // Assert
        config.UsePublisherConfirms.Should().BeTrue();
    }

    #endregion

    #region WithHeader Tests

    [Fact]
    public void WithHeader_ShouldAddHeader()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        config.WithHeader("key", "value");

        // Assert
        config.Headers.Should().ContainKey("key");
        config.Headers["key"].Should().Be("value");
    }

    [Fact]
    public void WithHeader_ShouldOverwriteExistingHeader()
    {
        // Arrange
        var config = new PublishConfiguration();
        config.WithHeader("key", "value1");

        // Act
        config.WithHeader("key", "value2");

        // Assert
        config.Headers.Should().ContainKey("key");
        config.Headers["key"].Should().Be("value2");
    }

    [Fact]
    public void WithHeader_ShouldThrowArgumentNullException_WhenKeyIsNull()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        Action act = () => config.WithHeader(null!, "value");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("key");
    }

    #endregion

    #region WithHeaders Tests

    [Fact]
    public void WithHeaders_ShouldAddMultipleHeaders()
    {
        // Arrange
        var config = new PublishConfiguration();
        var headers = new Dictionary<string, object>
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        };

        // Act
        config.WithHeaders(headers);

        // Assert
        config.Headers.Should().ContainKey("key1").And.ContainKey("key2");
        config.Headers["key1"].Should().Be("value1");
        config.Headers["key2"].Should().Be("value2");
    }

    [Fact]
    public void WithHeaders_ShouldMergeWithExistingHeaders()
    {
        // Arrange
        var config = new PublishConfiguration();
        config.WithHeader("existing", "old");
        var newHeaders = new Dictionary<string, object>
        {
            ["existing"] = "new",
            ["newKey"] = "newValue"
        };

        // Act
        config.WithHeaders(newHeaders);

        // Assert
        config.Headers.Should().HaveCount(2);
        config.Headers["existing"].Should().Be("new");
        config.Headers["newKey"].Should().Be("newValue");
    }

    [Fact]
    public void WithHeaders_ShouldThrowArgumentNullException_WhenHeadersIsNull()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        Action act = () => config.WithHeaders(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("headers");
    }

    #endregion

    #region WithExpiration Tests

    [Fact]
    public void WithExpiration_ShouldSetExpiration()
    {
        // Arrange
        var config = new PublishConfiguration();
        var expiration = TimeSpan.FromMinutes(5);

        // Act
        config.WithExpiration(expiration);

        // Assert
        config.Expiration.Should().Be(expiration);
    }

    [Fact]
    public void WithExpiration_ShouldThrowArgumentException_WhenExpirationIsZero()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        Action act = () => config.WithExpiration(TimeSpan.Zero);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Expiration must be positive*");
    }

    [Fact]
    public void WithExpiration_ShouldThrowArgumentException_WhenExpirationIsNegative()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        Action act = () => config.WithExpiration(TimeSpan.FromSeconds(-1));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Expiration must be positive*");
    }

    #endregion

    #region WithPriority Tests

    [Fact]
    public void WithPriority_ShouldSetPriority()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        config.WithPriority(5);

        // Assert
        config.Priority.Should().Be((byte)5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(9)]
    public void WithPriority_ShouldAcceptValidPriorityValues(byte priority)
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        config.WithPriority(priority);

        // Assert
        config.Priority.Should().Be(priority);
    }

    [Fact]
    public void WithPriority_ShouldThrowArgumentException_WhenPriorityIsGreaterThan9()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        Action act = () => config.WithPriority(10);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Priority must be between 0 and 9*");
    }

    #endregion

    #region WithCorrelationId Tests

    [Fact]
    public void WithCorrelationId_ShouldSetCorrelationId()
    {
        // Arrange
        var config = new PublishConfiguration();
        var correlationId = "corr-123";

        // Act
        config.WithCorrelationId(correlationId);

        // Assert
        config.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public void WithCorrelationId_ShouldThrowArgumentNullException_WhenCorrelationIdIsNull()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        Action act = () => config.WithCorrelationId(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("correlationId");
    }

    #endregion

    #region WithReplyTo Tests

    [Fact]
    public void WithReplyTo_ShouldSetReplyTo()
    {
        // Arrange
        var config = new PublishConfiguration();
        var replyTo = "reply-queue";

        // Act
        config.WithReplyTo(replyTo);

        // Assert
        config.ReplyTo.Should().Be(replyTo);
    }

    [Fact]
    public void WithReplyTo_ShouldThrowArgumentNullException_WhenReplyToIsNull()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        Action act = () => config.WithReplyTo(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("replyTo");
    }

    #endregion

    #region WithCustomProperty Tests

    [Fact]
    public void WithCustomProperty_ShouldAddCustomProperty()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        config.WithCustomProperty("customKey", "customValue");

        // Assert
        config.CustomProperties.Should().ContainKey("customKey");
        config.CustomProperties["customKey"].Should().Be("customValue");
    }

    [Fact]
    public void WithCustomProperty_ShouldOverwriteExistingCustomProperty()
    {
        // Arrange
        var config = new PublishConfiguration();
        config.WithCustomProperty("key", "value1");

        // Act
        config.WithCustomProperty("key", "value2");

        // Assert
        config.CustomProperties.Should().ContainKey("key");
        config.CustomProperties["key"].Should().Be("value2");
    }

    [Fact]
    public void WithCustomProperty_ShouldThrowArgumentNullException_WhenKeyIsNull()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        Action act = () => config.WithCustomProperty(null!, "value");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("key");
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ShouldReturnBasicInfo_WhenDefaultConfiguration()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        var result = config.ToString();

        // Assert
        result.Should().Be("Exchange: '', RoutingKey: ''");
    }

    [Fact]
    public void ToString_ShouldIncludeAllConfiguredProperties()
    {
        // Arrange
        var config = new PublishConfiguration();
        config.WithExchange("test-exchange");
        config.WithRoutingKey("test.key");
        config.WithMessageId("msg-123");
        config.WithMandatory(true);
        config.WithPublisherConfirms(false);
        config.WithExpiration(TimeSpan.FromMinutes(5));
        config.WithPriority(5);
        config.WithCorrelationId("corr-123");
        config.WithReplyTo("reply-queue");
        config.WithHeader("header1", "value1");
        config.WithCustomProperty("custom1", "customValue");

        // Act
        var result = config.ToString();

        // Assert
        result.Should().Contain("Exchange: 'test-exchange'");
        result.Should().Contain("RoutingKey: 'test.key'");
        result.Should().Contain("MessageId: msg-123");
        result.Should().Contain("Mandatory: true");
        result.Should().Contain("UsePublisherConfirms: false");
        result.Should().Contain("Expiration: 300000ms");
        result.Should().Contain("Priority: 5");
        result.Should().Contain("CorrelationId: corr-123");
        result.Should().Contain("ReplyTo: reply-queue");
        result.Should().Contain("Headers: 1");
        result.Should().Contain("CustomProps: 1");
    }

    #endregion

    #region CreateBasicProperties Tests

    [Fact]
    public void CreateBasicProperties_ShouldCreatePropertiesWithDefaults()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        var props = config.CreateBasicProperties();

        // Assert
        props.Should().NotBeNull();
        props.MessageId.Should().NotBeNullOrEmpty();
        props.Type.Should().BeNull();
        props.ContentType.Should().Be("application/json");
        props.ContentEncoding.Should().Be("utf-8");
        props.DeliveryMode.Should().Be(DeliveryModes.Persistent);
        props.Priority.Should().Be((byte)0);
        props.Timestamp.Should().NotBeNull();
        props.CorrelationId.Should().BeNull();
        props.ReplyTo.Should().BeNull();
        props.Expiration.Should().BeNull();
        props.Headers.Should().BeNull();
    }

    [Fact]
    public void CreateBasicProperties_ShouldIncludeConfiguredProperties()
    {
        // Arrange
        var config = new PublishConfiguration();
        config.WithMessageId("custom-msg-id");
        config.WithCorrelationId("corr-123");
        config.WithReplyTo("reply-queue");
        config.WithExpiration(TimeSpan.FromMinutes(5));
        config.WithPriority(7);
        config.WithHeader("header1", "value1");
        config.WithCustomProperty("custom1", "customValue");

        // Act
        var props = config.CreateBasicProperties();

        // Assert
        props.MessageId.Should().Be("custom-msg-id");
        props.CorrelationId.Should().Be("corr-123");
        props.ReplyTo.Should().Be("reply-queue");
        props.Expiration.Should().Be("300000");
        props.Priority.Should().Be((byte)7);
        props.Headers.Should().NotBeNull();
        props.Headers.Should().ContainKey("header1");
        props.Headers.Should().ContainKey("x-custom1");
        props.Headers["header1"].Should().Be("value1");
        props.Headers["x-custom1"].Should().Be("customValue");
    }

    [Fact]
    public void CreateBasicProperties_ShouldGenerateMessageId_WhenNotSet()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        var props1 = config.CreateBasicProperties();
        var props2 = config.CreateBasicProperties();

        // Assert
        props1.MessageId.Should().NotBeNullOrEmpty();
        props2.MessageId.Should().NotBeNullOrEmpty();
        props1.MessageId.Should().NotBe(props2.MessageId); // Should be different
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ShouldResetAllPropertiesToDefaults()
    {
        // Arrange
        var config = new PublishConfiguration();
        config.WithExchange("test-exchange");
        config.WithRoutingKey("test.key");
        config.WithMessageId("msg-123");
        config.WithMandatory(true);
        config.WithPublisherConfirms(false);
        config.WithExpiration(TimeSpan.FromMinutes(5));
        config.WithPriority(5);
        config.WithCorrelationId("corr-123");
        config.WithReplyTo("reply-queue");
        config.WithHeader("header1", "value1");
        config.WithCustomProperty("custom1", "customValue");

        // Act
        config.Reset();

        // Assert
        config.Exchange.Should().BeEmpty();
        config.RoutingKey.Should().BeEmpty();
        config.MessageId.Should().BeNull();
        config.Mandatory.Should().BeFalse();
        config.UsePublisherConfirms.Should().BeTrue();
        config.Expiration.Should().BeNull();
        config.Priority.Should().Be((byte)0);
        config.CorrelationId.Should().BeNull();
        config.ReplyTo.Should().BeNull();
        config.Headers.Should().BeEmpty();
        config.CustomProperties.Should().BeEmpty();
    }

    #endregion

    #region Fluent API Tests

    [Fact]
    public void Methods_ShouldAllowSequentialCalls()
    {
        // Arrange
        var config = new PublishConfiguration();

        // Act
        config.WithExchange("exchange");
        config.WithRoutingKey("routing.key");
        config.WithMessageId("msg-123");
        config.WithMandatory(true);
        config.WithExpiration(TimeSpan.FromMinutes(10));
        config.WithPriority(3);
        config.WithCorrelationId("corr-123");
        config.WithReplyTo("reply-queue");
        config.WithHeader("key", "value");

        // Assert
        config.Exchange.Should().Be("exchange");
        config.RoutingKey.Should().Be("routing.key");
        config.MessageId.Should().Be("msg-123");
        config.Mandatory.Should().BeTrue();
        config.Expiration.Should().Be(TimeSpan.FromMinutes(10));
        config.Priority.Should().Be((byte)3);
        config.CorrelationId.Should().Be("corr-123");
        config.ReplyTo.Should().Be("reply-queue");
        config.Headers.Should().ContainKey("key");
    }

    #endregion
}