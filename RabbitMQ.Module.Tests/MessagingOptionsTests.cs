using Xunit;
using FluentAssertions;
using RabbitMQ.Module.Configuration;
using RabbitMQ.Client;
using System.Net.Security;
using RabbitMQ.Module.Deduplication;

namespace RabbitMQ.Module.Tests;

public class MessagingOptionsTests
{
    #region ConnectionString Tests

    [Fact]
    public void ConnectionString_ShouldHaveDefaultValue()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        options.ConnectionString.Should().Be("amqp://guest:guest@localhost:5672/");
    }

    [Fact]
    public void ConnectionString_ShouldSetValidValue()
    {
        // Arrange
        var options = new MessagingOptions();
        var validUri = "amqp://user:pass@rabbitmq.example.com:5672/vhost";

        // Act
        options.ConnectionString = validUri;

        // Assert
        options.ConnectionString.Should().Be(validUri);
    }

    [Fact]
    public void ConnectionString_ShouldThrowArgumentNullException_WhenValueIsNull()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act
        Action act = () => options.ConnectionString = null!;

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("ConnectionString");
    }

    #endregion

    #region ClientProvidedName Tests

    [Fact]
    public void ClientProvidedName_ShouldHaveDefaultValue()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        options.ClientProvidedName.Should().Be("RabbitMQ.Module");
    }

    [Fact]
    public void ClientProvidedName_ShouldSetValidValue()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act
        options.ClientProvidedName = "MyApp";

        // Assert
        options.ClientProvidedName.Should().Be("MyApp");
    }

    [Fact]
    public void ClientProvidedName_ShouldThrowArgumentNullException_WhenValueIsNull()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act
        Action act = () => options.ClientProvidedName = null!;

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("ClientProvidedName");
    }

    #endregion

    #region RequestedHeartbeat Tests

    [Fact]
    public void RequestedHeartbeat_ShouldHaveDefaultValue()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        options.RequestedHeartbeat.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void RequestedHeartbeat_ShouldSetValidValue()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act
        options.RequestedHeartbeat = TimeSpan.FromSeconds(120);

        // Assert
        options.RequestedHeartbeat.Should().Be(TimeSpan.FromSeconds(120));
    }

    #endregion

    #region AutomaticRecoveryEnabled Tests

    [Fact]
    public void AutomaticRecoveryEnabled_ShouldHaveDefaultValueTrue()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        options.AutomaticRecoveryEnabled.Should().BeTrue();
    }

    [Fact]
    public void AutomaticRecoveryEnabled_ShouldSetValue()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act
        options.AutomaticRecoveryEnabled = false;

        // Assert
        options.AutomaticRecoveryEnabled.Should().BeFalse();
    }

    #endregion

    #region NetworkRecoveryInterval Tests

    [Fact]
    public void NetworkRecoveryInterval_ShouldHaveDefaultValue()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        options.NetworkRecoveryInterval.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void NetworkRecoveryInterval_ShouldSetValidValue()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act
        options.NetworkRecoveryInterval = TimeSpan.FromSeconds(10);

        // Assert
        options.NetworkRecoveryInterval.Should().Be(TimeSpan.FromSeconds(10));
    }

    #endregion

    #region ConnectionTimeout Tests

    [Fact]
    public void ConnectionTimeout_ShouldHaveDefaultValue()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        options.ConnectionTimeout.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void ConnectionTimeout_ShouldSetValidValue()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act
        options.ConnectionTimeout = TimeSpan.FromSeconds(30);

        // Assert
        options.ConnectionTimeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    #endregion

    #region RequestedChannelMax Tests

    [Fact]
    public void RequestedChannelMax_ShouldHaveDefaultValueZero()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        options.RequestedChannelMax.Should().Be(0);
    }

    [Fact]
    public void RequestedChannelMax_ShouldSetValue()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act
        options.RequestedChannelMax = 100;

        // Assert
        options.RequestedChannelMax.Should().Be(100);
    }

    #endregion

    #region RequestedFrameMax Tests

    [Fact]
    public void RequestedFrameMax_ShouldHaveDefaultValueZero()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        options.RequestedFrameMax.Should().Be(0U);
    }

    [Fact]
    public void RequestedFrameMax_ShouldSetValue()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act
        options.RequestedFrameMax = 131072;

        // Assert
        options.RequestedFrameMax.Should().Be(131072U);
    }

    #endregion

    #region MaxChannelsInPool Tests

    [Fact]
    public void MaxChannelsInPool_ShouldHaveDefaultValue()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        options.MaxChannelsInPool.Should().Be(50);
    }

    [Fact]
    public void MaxChannelsInPool_ShouldSetValidValue()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act
        options.MaxChannelsInPool = 100;

        // Assert
        options.MaxChannelsInPool.Should().Be(100);
    }

    #endregion

    #region PublisherConfirms Tests

    [Fact]
    public void PublisherConfirms_ShouldHaveDefaultValueFromDeliveryControl()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        options.PublisherConfirms.Should().Be(options.DeliveryControl.PublisherConfirmsEnabled);
    }

    [Fact]
    public void PublisherConfirms_ShouldSetValue()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act
        options.PublisherConfirms = true;

        // Assert
        options.PublisherConfirms.Should().BeTrue();
        options.DeliveryControl.PublisherConfirmsEnabled.Should().BeTrue();
    }

    #endregion

    #region ConnectionRetryCount Tests

    [Fact]
    public void ConnectionRetryCount_ShouldHaveDefaultValue()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        options.ConnectionRetryCount.Should().Be(5);
    }

    [Fact]
    public void ConnectionRetryCount_ShouldSetValidValue()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act
        options.ConnectionRetryCount = 3;

        // Assert
        options.ConnectionRetryCount.Should().Be(3);
    }

    #endregion

    #region ConnectionRetryDelay Tests

    [Fact]
    public void ConnectionRetryDelay_ShouldHaveDefaultValue()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        options.ConnectionRetryDelay.Should().Be(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ConnectionRetryDelay_ShouldSetValue()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act
        options.ConnectionRetryDelay = TimeSpan.FromSeconds(2);

        // Assert
        options.ConnectionRetryDelay.Should().Be(TimeSpan.FromSeconds(2));
    }

    #endregion

    #region QueuePrefix Tests

    [Fact]
    public void QueuePrefix_ShouldHaveDefaultValue()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        options.QueuePrefix.Should().BeEmpty();
    }

    [Fact]
    public void QueuePrefix_ShouldSetValidValue()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act
        options.QueuePrefix = "dev";

        // Assert
        options.QueuePrefix.Should().Be("dev");
    }

    [Fact]
    public void QueuePrefix_ShouldAcceptNull()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act
        options.QueuePrefix = null!;

        // Assert
        options.QueuePrefix.Should().BeEmpty();
    }

    [Theory]
    [InlineData("dev")]
    [InlineData("prod_env")]
    [InlineData("test-123")]
    [InlineData("MyApp_v2")]
    public void QueuePrefix_ShouldAcceptValidCharacters(string validPrefix)
    {
        // Arrange
        var options = new MessagingOptions();

        // Act
        options.QueuePrefix = validPrefix;

        // Assert
        options.QueuePrefix.Should().Be(validPrefix);
    }

    [Theory]
    [InlineData("dev env")]
    [InlineData("dev@env")]
    [InlineData("dev.env")]
    [InlineData("dev/env")]
    public void QueuePrefix_ShouldThrowArgumentException_WhenContainsInvalidCharacters(string invalidPrefix)
    {
        // Arrange
        var options = new MessagingOptions();

        // Act
        Action act = () => options.QueuePrefix = invalidPrefix;

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*может содержать только буквы, цифры, дефис и подчеркивание*");
    }

    #endregion

    #region MaxMessageSize Tests

    [Fact]
    public void MaxMessageSize_ShouldHaveDefaultValueZero()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        options.MaxMessageSize.Should().Be(0U);
    }

    [Fact]
    public void MaxMessageSize_ShouldSetValidValue()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act
        options.MaxMessageSize = 1024;

        // Assert
        options.MaxMessageSize.Should().Be(1024U);
    }

    #endregion

    #region ContinuationTimeout Tests

    [Fact]
    public void ContinuationTimeout_ShouldHaveDefaultValue()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        options.ContinuationTimeout.Should().Be(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void ContinuationTimeout_ShouldSetValue()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act
        options.ContinuationTimeout = TimeSpan.FromSeconds(30);

        // Assert
        options.ContinuationTimeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    #endregion

    #region SslEnabled Tests

    [Fact]
    public void SslEnabled_ShouldHaveDefaultValueFalse()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        options.SslEnabled.Should().BeFalse();
    }

    [Fact]
    public void SslEnabled_ShouldSetValue()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act
        options.SslEnabled = true;

        // Assert
        options.SslEnabled.Should().BeTrue();
    }

    #endregion

    #region SslOption Tests

    [Fact]
    public void SslOption_ShouldBeNullByDefault()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        options.SslOption.Should().BeNull();
    }

    [Fact]
    public void SslOption_ShouldSetValue()
    {
        // Arrange
        var options = new MessagingOptions();
        var sslOption = new SslOption();

        // Act
        options.SslOption = sslOption;

        // Assert
        options.SslOption.Should().Be(sslOption);
    }

    #endregion

    #region AcceptableSslErrors Tests

    [Fact]
    public void AcceptableSslErrors_ShouldHaveDefaultValue()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        options.AcceptableSslErrors.Should().Be(SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors);
    }

    [Fact]
    public void AcceptableSslErrors_ShouldSetValue()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act
        options.AcceptableSslErrors = SslPolicyErrors.None;

        // Assert
        options.AcceptableSslErrors.Should().Be(SslPolicyErrors.None);
    }

    #endregion

    #region VirtualHost Tests

    [Fact]
    public void VirtualHost_ShouldReturnDefaultVHost()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act & Assert
        options.VirtualHost.Should().Be("/");
    }

    [Fact]
    public void VirtualHost_ShouldExtractFromConnectionString()
    {
        // Arrange
        var options = new MessagingOptions
        {
            ConnectionString = "amqp://guest:guest@localhost:5672/myvhost"
        };

        // Act & Assert
        options.VirtualHost.Should().Be("myvhost");
    }

    [Fact]
    public void VirtualHost_ShouldReturnFallback_WhenConnectionStringInvalid()
    {
        // Arrange
        var options = new MessagingOptions
        {
            ConnectionString = "invalid-uri"
        };

        // Act & Assert
        options.VirtualHost.Should().Be("/");
    }

    #endregion

    #region HostName Tests

    [Fact]
    public void HostName_ShouldReturnDefaultHost()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act & Assert
        options.HostName.Should().Be("localhost");
    }

    [Fact]
    public void HostName_ShouldExtractFromConnectionString()
    {
        // Arrange
        var options = new MessagingOptions
        {
            ConnectionString = "amqp://guest:guest@rabbitmq.example.com:5672/"
        };

        // Act & Assert
        options.HostName.Should().Be("rabbitmq.example.com");
    }

    [Fact]
    public void HostName_ShouldReturnFallback_WhenConnectionStringInvalid()
    {
        // Arrange
        var options = new MessagingOptions
        {
            ConnectionString = "invalid-uri"
        };

        // Act & Assert
        options.HostName.Should().Be("localhost");
    }

    #endregion

    #region Port Tests

    [Fact]
    public void Port_ShouldReturnDefaultPort()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act & Assert
        options.Port.Should().Be(5672);
    }

    [Fact]
    public void Port_ShouldReturnSslPort_WhenSslEnabled()
    {
        // Arrange
        var options = new MessagingOptions
        {
            ConnectionString = "invalid-uri", // Force fallback to SslEnabled check
            SslEnabled = true
        };

        // Act & Assert
        options.Port.Should().Be(5671);
    }

    [Fact]
    public void Port_ShouldExtractFromConnectionString()
    {
        // Arrange
        var options = new MessagingOptions
        {
            ConnectionString = "amqp://guest:guest@localhost:9999/"
        };

        // Act & Assert
        options.Port.Should().Be(9999);
    }

    [Fact]
    public void Port_ShouldReturnFallback_WhenConnectionStringInvalid()
    {
        // Arrange
        var options = new MessagingOptions
        {
            ConnectionString = "invalid-uri"
        };

        // Act & Assert
        options.Port.Should().Be(5672);
    }

    #endregion

    #region UserName Tests

    [Fact]
    public void UserName_ShouldReturnDefaultUser()
    {
        // Arrange
        var options = new MessagingOptions();

        // Act & Assert
        options.UserName.Should().Be("guest");
    }

    [Fact]
    public void UserName_ShouldExtractFromConnectionString()
    {
        // Arrange
        var options = new MessagingOptions
        {
            ConnectionString = "amqp://admin:secret@localhost:5672/"
        };

        // Act & Assert
        options.UserName.Should().Be("admin");
    }

    [Fact]
    public void UserName_ShouldReturnFallback_WhenConnectionStringInvalid()
    {
        // Arrange
        var options = new MessagingOptions
        {
            ConnectionString = "invalid-uri"
        };

        // Act & Assert
        options.UserName.Should().Be("guest");
    }

    #endregion

    #region DeliveryControl Tests

    [Fact]
    public void DeliveryControl_ShouldHaveDefaultInstance()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        options.DeliveryControl.Should().NotBeNull();
        options.DeliveryControl.Should().BeOfType<DeliveryControlOptions>();
    }

    #endregion

    #region Deduplication Tests

    [Fact]
    public void Deduplication_ShouldHaveDefaultInstance()
    {
        // Arrange & Act
        var options = new MessagingOptions();

        // Assert
        options.Deduplication.Should().NotBeNull();
        options.Deduplication.Should().BeOfType<DeduplicationOptions>();
    }

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_ShouldPass_WhenAllPropertiesValid()
    {
        // Arrange
        var options = new MessagingOptions
        {
            ConnectionString = "amqp://guest:guest@localhost:5672/",
            ClientProvidedName = "TestApp",
            RequestedHeartbeat = TimeSpan.FromSeconds(60),
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
            ConnectionTimeout = TimeSpan.FromSeconds(10),
            MaxChannelsInPool = 50,
            ConnectionRetryCount = 5,
            QueuePrefix = "test",
            MaxMessageSize = 0,
            ContinuationTimeout = TimeSpan.FromSeconds(20),
            SslEnabled = false
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_ShouldThrowInvalidOperationException_WhenConnectionStringEmpty()
    {
        // Arrange
        var options = new MessagingOptions
        {
            ConnectionString = ""
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ConnectionString не может быть пустым*");
    }

    [Fact]
    public void Validate_ShouldThrowInvalidOperationException_WhenConnectionStringInvalidScheme()
    {
        // Arrange
        var options = new MessagingOptions
        {
            ConnectionString = "http://guest:guest@localhost:5672/"
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ConnectionString должна использовать amqp:// или amqps://*");
    }

    [Fact]
    public void Validate_ShouldThrowInvalidOperationException_WhenConnectionStringNoCredentials()
    {
        // Arrange
        var options = new MessagingOptions
        {
            ConnectionString = "amqp://localhost:5672/"
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ConnectionString должна содержать логин и пароль*");
    }

    [Fact]
    public void Validate_ShouldThrowInvalidOperationException_WhenMaxChannelsInPoolInvalid()
    {
        // Arrange
        var options = new MessagingOptions
        {
            MaxChannelsInPool = 0
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*MaxChannelsInPool должен быть больше 0*");
    }

    [Fact]
    public void Validate_ShouldThrowInvalidOperationException_WhenConnectionRetryCountInvalid()
    {
        // Arrange
        var options = new MessagingOptions
        {
            ConnectionRetryCount = 0
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ConnectionRetryCount должен быть больше 0*");
    }

    [Fact]
    public void Validate_ShouldThrowInvalidOperationException_WhenRequestedHeartbeatTooSmall()
    {
        // Arrange
        var options = new MessagingOptions
        {
            RequestedHeartbeat = TimeSpan.FromSeconds(5)
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*RequestedHeartbeat должен быть не менее 10 секунд*");
    }

    [Fact]
    public void Validate_ShouldThrowInvalidOperationException_WhenNetworkRecoveryIntervalTooSmall()
    {
        // Arrange
        var options = new MessagingOptions
        {
            NetworkRecoveryInterval = TimeSpan.FromMilliseconds(500)
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*NetworkRecoveryInterval должен быть не менее 1 секунды*");
    }

    [Fact]
    public void Validate_ShouldThrowInvalidOperationException_WhenConnectionTimeoutTooSmall()
    {
        // Arrange
        var options = new MessagingOptions
        {
            ConnectionTimeout = TimeSpan.FromMilliseconds(500)
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ConnectionTimeout должен быть не менее 1 секунды*");
    }

    [Fact]
    public void Validate_ShouldThrowInvalidOperationException_WhenMaxMessageSizeTooSmall()
    {
        // Arrange
        var options = new MessagingOptions
        {
            MaxMessageSize = 512
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*MaxMessageSize должен быть не менее 1024 байт*");
    }

    [Fact]
    public void Validate_ShouldThrowInvalidOperationException_WhenSslEnabledButSslOptionNull()
    {
        // Arrange
        var options = new MessagingOptions
        {
            SslEnabled = true,
            SslOption = null
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SslOption должен быть указан при включенном SSL*");
    }

    #endregion

    #region CreateConnectionFactory Tests

    [Fact]
    public void CreateConnectionFactory_ShouldCreateFactoryWithCorrectSettings()
    {
        // Arrange
        var options = new MessagingOptions
        {
            ConnectionString = "amqp://guest:guest@localhost:5672/",
            ClientProvidedName = "TestApp",
            RequestedHeartbeat = TimeSpan.FromSeconds(60),
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
            ContinuationTimeout = TimeSpan.FromSeconds(20),
            RequestedChannelMax = 100,
            RequestedFrameMax = 131072
        };

        // Act
        var factory = options.CreateConnectionFactory();

        // Assert
        factory.Should().NotBeNull();
        factory.ClientProvidedName.Should().Be("TestApp");
        factory.RequestedHeartbeat.Should().Be(TimeSpan.FromSeconds(60));
        factory.AutomaticRecoveryEnabled.Should().BeTrue();
        factory.NetworkRecoveryInterval.Should().Be(TimeSpan.FromSeconds(5));
        factory.ContinuationTimeout.Should().Be(TimeSpan.FromSeconds(20));
        factory.RequestedChannelMax.Should().Be(100);
        factory.RequestedFrameMax.Should().Be(131072U);
    }

    [Fact]
    public void CreateConnectionFactory_ShouldConfigureSsl_WhenSslEnabled()
    {
        // Arrange
        var options = new MessagingOptions
        {
            ConnectionString = "amqps://guest:guest@localhost:5671/",
            SslEnabled = true,
            SslOption = new SslOption
            {
                Enabled = true,
                ServerName = "localhost"
            }
        };

        // Act
        var factory = options.CreateConnectionFactory();

        // Assert
        factory.Ssl.Should().NotBeNull();
        factory.Ssl.Enabled.Should().BeTrue();
        factory.Ssl.ServerName.Should().Be("localhost");
    }

    #endregion

    #region Clone Tests

    [Fact]
    public void Clone_ShouldCreateDeepCopy()
    {
        // Arrange
        var original = new MessagingOptions
        {
            ConnectionString = "amqp://user:pass@host:5672/vhost",
            ClientProvidedName = "TestApp",
            QueuePrefix = "prefix",
            RequestedHeartbeat = TimeSpan.FromSeconds(120),
            AutomaticRecoveryEnabled = false,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
            ConnectionTimeout = TimeSpan.FromSeconds(20),
            RequestedChannelMax = 200,
            RequestedFrameMax = 262144,
            MaxChannelsInPool = 100,
            PublisherConfirms = true,
            ConnectionRetryCount = 3,
            ConnectionRetryDelay = TimeSpan.FromSeconds(2),
            MaxMessageSize = 2048,
            ContinuationTimeout = TimeSpan.FromSeconds(30),
            SslEnabled = true,
            SslOption = new SslOption { ServerName = "test.com" },
            AcceptableSslErrors = SslPolicyErrors.None
        };

        // Act
        var clone = original.Clone();

        // Assert
        clone.Should().NotBeSameAs(original);
        clone.ConnectionString.Should().Be(original.ConnectionString);
        clone.ClientProvidedName.Should().Be(original.ClientProvidedName);
        clone.QueuePrefix.Should().Be(original.QueuePrefix);
        clone.RequestedHeartbeat.Should().Be(original.RequestedHeartbeat);
        clone.AutomaticRecoveryEnabled.Should().Be(original.AutomaticRecoveryEnabled);
        clone.NetworkRecoveryInterval.Should().Be(original.NetworkRecoveryInterval);
        clone.ConnectionTimeout.Should().Be(original.ConnectionTimeout);
        clone.RequestedChannelMax.Should().Be(original.RequestedChannelMax);
        clone.RequestedFrameMax.Should().Be(original.RequestedFrameMax);
        clone.MaxChannelsInPool.Should().Be(original.MaxChannelsInPool);
        clone.PublisherConfirms.Should().Be(original.PublisherConfirms);
        clone.ConnectionRetryCount.Should().Be(original.ConnectionRetryCount);
        clone.ConnectionRetryDelay.Should().Be(original.ConnectionRetryDelay);
        clone.MaxMessageSize.Should().Be(original.MaxMessageSize);
        clone.ContinuationTimeout.Should().Be(original.ContinuationTimeout);
        clone.SslEnabled.Should().Be(original.SslEnabled);
        clone.SslOption.Should().NotBeNull();
        clone.SslOption.ServerName.Should().Be("test.com");
        clone.AcceptableSslErrors.Should().Be(original.AcceptableSslErrors);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ShouldReturnFormattedString()
    {
        // Arrange
        var options = new MessagingOptions
        {
            ConnectionString = "amqp://user:pass@localhost:5672/myvhost",
            ClientProvidedName = "TestApp",
            RequestedHeartbeat = TimeSpan.FromSeconds(120),
            MaxChannelsInPool = 100,
            ConnectionRetryCount = 3
        };

        // Act
        var result = options.ToString();

        // Assert
        result.Should().Contain("Host: localhost:5672");
        result.Should().Contain("VHost: myvhost");
        result.Should().Contain("User: user");
        result.Should().Contain("SSL: False");
        result.Should().Contain("Channels in pool: 100");
        result.Should().Contain("ChannelMax: 0");
        result.Should().Contain("Heartbeat: 120s");
        result.Should().Contain("RetryCount: 3");
    }

    [Fact]
    public void ToString_ShouldHandleInvalidConnectionString()
    {
        // Arrange
        var options = new MessagingOptions
        {
            ConnectionString = "invalid-uri"
        };

        // Act
        var result = options.ToString();

        // Assert
        result.Should().Contain("ConnectionString: [invalid]");
        result.Should().Contain("SSL: False");
    }

    #endregion
}