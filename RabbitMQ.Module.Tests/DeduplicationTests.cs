namespace RabbitMQ.Module.Tests;

using Contracts;

using Deduplication;

using DeliveryControl;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

public class InMemoryDeduplicationStoreTests
{

    #region Fields

    private readonly IDeliveryMetrics _metrics = new DefaultDeliveryMetrics(NullLogger<DefaultDeliveryMetrics>.Instance);

    #endregion

    #region Methods

    [Fact]
    public async Task TryAddAsync_ShouldReturnTrue_ForFirstAdd()
    {
        var options = new DeduplicationOptions
        {
            Ttl = TimeSpan.FromHours(1)
        };

        var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new InMemoryDeduplicationStore(
            cache,
            NullLogger<InMemoryDeduplicationStore>.Instance,
            options,
            _metrics); // ✅ Добавляем metrics

        bool result = await store.TryAddAsync("msg-1", TimeSpan.FromHours(1));

        Assert.True(result);
    }

    [Fact]
    public async Task TryAddAsync_ShouldReturnFalse_ForDuplicate()
    {
        var options = new DeduplicationOptions
        {
            Ttl = TimeSpan.FromHours(1)
        };

        var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new InMemoryDeduplicationStore(
            cache,
            NullLogger<InMemoryDeduplicationStore>.Instance,
            options,
            _metrics); // ✅ Добавляем metrics

        await store.TryAddAsync("msg-1", TimeSpan.FromHours(1));
        bool result = await store.TryAddAsync("msg-1", TimeSpan.FromHours(1));

        Assert.False(result);
    }

    [Fact]
    public async Task RemoveAsync_ShouldAllowReAdd()
    {
        var options = new DeduplicationOptions
        {
            Ttl = TimeSpan.FromHours(1)
        };

        var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new InMemoryDeduplicationStore(
            cache,
            NullLogger<InMemoryDeduplicationStore>.Instance,
            options,
            _metrics); // ✅ Добавляем metrics

        await store.TryAddAsync("msg-1", TimeSpan.FromHours(1));
        await store.RemoveAsync("msg-1");
        bool result = await store.TryAddAsync("msg-1", TimeSpan.FromHours(1));

        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_ForExistingKey()
    {
        var options = new DeduplicationOptions
        {
            Ttl = TimeSpan.FromHours(1)
        };

        var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new InMemoryDeduplicationStore(
            cache,
            NullLogger<InMemoryDeduplicationStore>.Instance,
            options,
            _metrics); // ✅ Добавляем metrics

        await store.TryAddAsync("msg-1", TimeSpan.FromHours(1));
        bool exists = await store.ExistsAsync("msg-1");

        Assert.True(exists);
    }

    [Fact]
    public async Task TryAddAsync_ShouldExpire_AfterTtl()
    {
        var options = new DeduplicationOptions
        {
            Ttl = TimeSpan.FromMilliseconds(100)
        };

        var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new InMemoryDeduplicationStore(
            cache,
            NullLogger<InMemoryDeduplicationStore>.Instance,
            options,
            _metrics); // ✅ Добавляем metrics

        await store.TryAddAsync("msg-1", TimeSpan.FromMilliseconds(100));
        Assert.True(await store.ExistsAsync("msg-1"));

        await Task.Delay(150);
        Assert.False(await store.ExistsAsync("msg-1"));
    }

    #endregion

}
