namespace RabbitMQ.Module.Deduplication;

using Contracts;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

/// <summary>
/// In-memory реализация хранилища дедубликации
/// </summary>
public class InMemoryDeduplicationStore(
    IMemoryCache cache,
    ILogger<InMemoryDeduplicationStore> logger,
    DeduplicationOptions options,
    IDeliveryMetrics? metrics = null)
    : IDeduplicationStore
{

    #region Constants

    private const string STORE_TYPE = "InMemory";

    #endregion

    #region Fields

    private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private readonly ILogger<InMemoryDeduplicationStore> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly DeduplicationOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly IDeliveryMetrics? _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));

    #endregion

    #region Methods

    public Task<bool> TryAddAsync(string messageId, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        try
        {
            string key = GetKey(messageId);

            MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(ttl)
                .SetPriority(CacheItemPriority.Normal)
                .RegisterPostEvictionCallback(OnEntryEvicted);

            object? existing = _cache.Get(key);

            if (existing != null)
            {
                _logger.LogTrace("Message {MessageId} already exists in deduplication store", messageId);
                return Task.FromResult(false);
            }

            _cache.Set(
                key,
                new DeduplicationEntry
                {
                    MessageId = messageId,
                    ProcessedAt = DateTime.UtcNow,
                    Ttl = ttl
                },
                cacheEntryOptions);

            _logger.LogTrace("Message {MessageId} added to deduplication store, TTL: {Ttl}", messageId, ttl);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InMemory error during TryAdd for {MessageId}", messageId);
            _metrics?.DeduplicationStoreError(STORE_TYPE, ex.Message);
            throw;
        }
    }

    public Task RemoveAsync(string messageId, CancellationToken cancellationToken = default)
    {
        string key = GetKey(messageId);
        _cache.Remove(key);
        _logger.LogTrace("Message {MessageId} removed from deduplication store", messageId);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string messageId, CancellationToken cancellationToken = default)
    {
        string key = GetKey(messageId);
        bool exists = _cache.Get(key) != null;
        return Task.FromResult(exists);
    }

    public Task<IReadOnlyCollection<string>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("GetAllAsync is not fully supported for MemoryCache, returning empty list");
        return Task.FromResult<IReadOnlyCollection<string>>(Array.Empty<string>());
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("ClearAsync is not fully supported for MemoryCache, disposing cache");

        if (_cache is IDisposable disposable)
        {
            disposable.Dispose();
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (_cache is IDisposable disposable)
        {
            disposable.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private string GetKey(string messageId)
    {
        return $"{_options.KeyPrefix}{messageId}";
    }

    private void OnEntryEvicted(object key, object? value, EvictionReason reason, object? state)
    {
        if (value is DeduplicationEntry entry)
        {
            _logger.LogTrace(
                "Entry {MessageId} evicted from cache, reason: {Reason}",
                entry.MessageId,
                reason);
        }
    }

    #endregion

}
