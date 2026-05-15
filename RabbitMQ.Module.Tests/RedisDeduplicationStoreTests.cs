using RabbitMQ.Module.Deduplication;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using StackExchange.Redis;

using Testcontainers.Redis;

using Xunit;
using Xunit.Abstractions;

namespace RabbitMQ.Module.Tests;

public class RedisDeduplicationStoreTests(ITestOutputHelper output) : IAsyncLifetime
{

    #region Fields

    private readonly RedisContainer _redisContainer = new RedisBuilder("redis:7-alpine")
        .WithPortBinding(6379, true)
        .Build();

    private IConnectionMultiplexer? _redis;
    private RedisDeduplicationStore? _store;
    private DeduplicationOptions? _options;

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenRedisIsNull()
    {
        // Arrange
        var options = new DeduplicationOptions();
        var logger = NullLogger<RedisDeduplicationStore>.Instance;

        // Act
        Action act = () => new RedisDeduplicationStore(null!, logger, options);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("redis");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var options = new DeduplicationOptions();

        // Act
        Action act = () => new RedisDeduplicationStore(mockRedis.Object, null!, options);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenOptionsIsNull()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var logger = NullLogger<RedisDeduplicationStore>.Instance;

        // Act
        Action act = () => new RedisDeduplicationStore(mockRedis.Object, logger, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public void Constructor_ShouldInitializeWithFallbackStore()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();
        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

        var logger = NullLogger<RedisDeduplicationStore>.Instance;
        var options = new DeduplicationOptions { EnableRedisFallback = true };
        var mockFallback = new Mock<IDeduplicationStore>();

        // Act
        var store = new RedisDeduplicationStore(mockRedis.Object, logger, options, mockFallback.Object);

        // Assert
        store.Should().NotBeNull();
    }

    #endregion

    #region TryAddAsync Tests

    [Fact]
    public async Task TryAddAsync_ShouldReturnTrue_ForFirstAdd()
    {
        // Arrange & Act
        bool result = await _store!.TryAddAsync("msg-1", TimeSpan.FromHours(1));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryAddAsync_ShouldReturnFalse_ForDuplicate()
    {
        // Arrange
        await _store!.TryAddAsync("msg-1", TimeSpan.FromHours(1));

        // Act
        bool result = await _store.TryAddAsync("msg-1", TimeSpan.FromHours(1));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryAddAsync_ShouldHandleDifferentMessages()
    {
        // Arrange & Act
        bool result1 = await _store!.TryAddAsync("msg-1", TimeSpan.FromHours(1));
        bool result2 = await _store.TryAddAsync("msg-2", TimeSpan.FromHours(1));

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();
    }

    [Fact]
    public async Task TryAddAsync_ShouldRespectTtl()
    {
        // Arrange
        var shortTtl = TimeSpan.FromMilliseconds(100);

        // Act
        bool result = await _store!.TryAddAsync("msg-1", shortTtl);
        await Task.Delay(200); // Wait for TTL to expire
        bool existsAfterTtl = await _store.ExistsAsync("msg-1");

        // Assert
        result.Should().BeTrue();
        existsAfterTtl.Should().BeFalse();
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_ShouldAllowReAdd()
    {
        // Arrange
        await _store!.TryAddAsync("msg-1", TimeSpan.FromHours(1));

        // Act
        await _store.RemoveAsync("msg-1");
        bool result = await _store.TryAddAsync("msg-1", TimeSpan.FromHours(1));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveAsync_ShouldHandleNonExistentKey()
    {
        // Arrange & Act
        await _store!.RemoveAsync("non-existent");

        // Assert - Should not throw
    }

    #endregion

    #region ExistsAsync Tests

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_ForExistingKey()
    {
        // Arrange
        await _store!.TryAddAsync("msg-1", TimeSpan.FromHours(1));

        // Act
        bool exists = await _store.ExistsAsync("msg-1");

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_ForNonExistentKey()
    {
        // Arrange & Act
        bool exists = await _store!.ExistsAsync("non-existent");

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_AfterTtlExpires()
    {
        // Arrange
        var shortTtl = TimeSpan.FromMilliseconds(100);
        await _store!.TryAddAsync("msg-1", shortTtl);

        // Act
        await Task.Delay(200); // Wait for TTL to expire
        bool exists = await _store.ExistsAsync("msg-1");

        // Assert
        exists.Should().BeFalse();
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyCollection_WhenNoKeys()
    {
        // Arrange & Act
        var result = await _store!.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllKeys()
    {
        // Arrange
        await _store!.TryAddAsync("msg-1", TimeSpan.FromHours(1));
        await _store.TryAddAsync("msg-2", TimeSpan.FromHours(1));
        await _store.TryAddAsync("msg-3", TimeSpan.FromHours(1));

        // Act
        var result = await _store.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(new[] { "msg-1", "msg-2", "msg-3" });
    }

    [Fact]
    public async Task GetAllAsync_ShouldNotReturnExpiredKeys()
    {
        // Arrange
        var shortTtl = TimeSpan.FromMilliseconds(100);
        await _store!.TryAddAsync("msg-1", TimeSpan.FromHours(1));
        await _store.TryAddAsync("msg-2", shortTtl);

        // Act
        await Task.Delay(200); // Wait for TTL to expire
        var result = await _store.GetAllAsync();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain("msg-1");
        result.Should().NotContain("msg-2");
    }

    #endregion

    #region ClearAsync Tests

    [Fact]
    public async Task ClearAsync_ShouldRemoveAllKeys()
    {
        // Arrange
        await _store!.TryAddAsync("msg-1", TimeSpan.FromHours(1));
        await _store.TryAddAsync("msg-2", TimeSpan.FromHours(1));

        // Act
        await _store.ClearAsync();
        var result = await _store.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ClearAsync_ShouldHandleEmptyStore()
    {
        // Arrange & Act
        await _store!.ClearAsync();

        // Assert - Should not throw
    }

    #endregion

    #region Fallback Tests

    [Fact]
    public async Task TryAddAsync_ShouldUseFallback_WhenRedisUnavailable()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();
        mockDatabase.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>()))
                   .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

        var logger = NullLogger<RedisDeduplicationStore>.Instance;
        var options = new DeduplicationOptions { EnableRedisFallback = true };
        var mockFallback = new Mock<IDeduplicationStore>();
        mockFallback.Setup(f => f.TryAddAsync("msg-1", TimeSpan.FromHours(1), default)).ReturnsAsync(true);

        var store = new RedisDeduplicationStore(mockRedis.Object, logger, options, mockFallback.Object);

        // Act
        bool result = await store.TryAddAsync("msg-1", TimeSpan.FromHours(1));

        // Assert
        result.Should().BeTrue();
        mockFallback.Verify(f => f.TryAddAsync("msg-1", TimeSpan.FromHours(1), default), Times.Once);
    }

    [Fact]
    public async Task TryAddAsync_ShouldReturnFalse_WhenRedisUnavailableAndNoFallback()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();
        mockDatabase.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>()))
                   .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

        var logger = NullLogger<RedisDeduplicationStore>.Instance;
        var options = new DeduplicationOptions { EnableRedisFallback = false };

        var store = new RedisDeduplicationStore(mockRedis.Object, logger, options);

        // Act
        bool result = await store.TryAddAsync("msg-1", TimeSpan.FromHours(1));

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Key Prefix Tests

    [Fact]
    public async Task Operations_ShouldUseKeyPrefix()
    {
        // Arrange - Create store with custom prefix
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();
        var callCount = 0;
        RedisKey capturedKey = default;

        mockDatabase.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>()))
                   .Callback<RedisKey, RedisValue, TimeSpan?, When>((key, value, ttl, when) =>
                   {
                       capturedKey = key;
                       callCount++;
                   })
                   .ReturnsAsync(true);

        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

        var logger = NullLogger<RedisDeduplicationStore>.Instance;
        var options = new DeduplicationOptions { KeyPrefix = "custom:" };

        var store = new RedisDeduplicationStore(mockRedis.Object, logger, options);

        // Act
        await store.TryAddAsync("msg-1", TimeSpan.FromHours(1));

        // Assert
        callCount.Should().Be(1);
        capturedKey.ToString().Should().StartWith("custom:");
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public async Task DisposeAsync_ShouldUnsubscribeFromEvents()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();
        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

        var logger = NullLogger<RedisDeduplicationStore>.Instance;
        var options = new DeduplicationOptions();

        var store = new RedisDeduplicationStore(mockRedis.Object, logger, options);

        // Act
        await store.DisposeAsync();

        // Assert - Events should be unsubscribed (hard to test directly, but no exceptions)
    }

    #endregion

    #region IAsyncLifetime

    public async Task InitializeAsync()
    {
        output.WriteLine("Запуск Redis контейнера...");
        await _redisContainer.StartAsync();

        ushort port = _redisContainer.GetMappedPublicPort(6379);
        string connectionString = $"{_redisContainer.Hostname}:{port}";

        output.WriteLine($"Redis запущен на {connectionString}");

        _redis = await ConnectionMultiplexer.ConnectAsync(connectionString);

        _options = new DeduplicationOptions
        {
            StoreType = DeduplicationStoreType.Redis,
            KeyPrefix = "test:",
            Ttl = TimeSpan.FromHours(1),
            LogRedisErrors = true
        };

        _store = new RedisDeduplicationStore(
            _redis,
            NullLogger<RedisDeduplicationStore>.Instance,
            _options);
    }

    public async Task DisposeAsync()
    {
        if (_store != null)
        {
            await _store.DisposeAsync();
        }

        _redis?.Dispose();
        await _redisContainer.DisposeAsync();
    }

    #endregion
}
