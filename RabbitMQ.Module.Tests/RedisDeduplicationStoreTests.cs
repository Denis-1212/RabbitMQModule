namespace RabbitMQ.Module.Tests;

using Deduplication;

using Microsoft.Extensions.Logging.Abstractions;

using StackExchange.Redis;

using Testcontainers.Redis;

using Xunit.Abstractions;

public class RedisDeduplicationStoreTests(ITestOutputHelper output) : IAsyncLifetime
{

    #region Fields

    private readonly RedisContainer _redisContainer = new RedisBuilder("redis:7-alpine")
        .WithPortBinding(6379, true)
        .Build();

    private IConnectionMultiplexer? _redis;
    private RedisDeduplicationStore? _store;

    #endregion

    #region Methods

    [Fact]
    public async Task TryAddAsync_ShouldReturnTrue_ForFirstAdd()
    {
        bool result = await _store!.TryAddAsync("msg-1", TimeSpan.FromHours(1));
        Assert.True(result);
    }

    [Fact]
    public async Task TryAddAsync_ShouldReturnFalse_ForDuplicate()
    {
        await _store!.TryAddAsync("msg-1", TimeSpan.FromHours(1));
        bool result = await _store.TryAddAsync("msg-1", TimeSpan.FromHours(1));
        Assert.False(result);
    }

    [Fact]
    public async Task RemoveAsync_ShouldAllowReAdd()
    {
        await _store!.TryAddAsync("msg-1", TimeSpan.FromHours(1));
        await _store.RemoveAsync("msg-1");
        bool result = await _store.TryAddAsync("msg-1", TimeSpan.FromHours(1));
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_ForExistingKey()
    {
        await _store!.TryAddAsync("msg-1", TimeSpan.FromHours(1));
        bool exists = await _store.ExistsAsync("msg-1");
        Assert.True(exists);
    }

    public async Task InitializeAsync()
    {
        output.WriteLine("Запуск Redis контейнера...");
        await _redisContainer.StartAsync();

        ushort port = _redisContainer.GetMappedPublicPort(6379);
        string connectionString = $"{_redisContainer.Hostname}:{port}";

        output.WriteLine($"Redis запущен на {connectionString}");

        _redis = await ConnectionMultiplexer.ConnectAsync(connectionString);

        var options = new DeduplicationOptions
        {
            StoreType = DeduplicationStoreType.Redis,
            KeyPrefix = "test:",
            Ttl = TimeSpan.FromHours(1),
            LogRedisErrors = true
        };

        _store = new RedisDeduplicationStore(
            _redis,
            NullLogger<RedisDeduplicationStore>.Instance,
            options);
    }

    public async Task DisposeAsync()
    {
        _store?.DisposeAsync();
        _redis?.Dispose();
        await _redisContainer.DisposeAsync();
    }

    #endregion

}
