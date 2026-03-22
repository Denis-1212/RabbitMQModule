namespace RabbitMQ.Module.Deduplication;

using System.Net;
using System.Text.Json;

using Contracts;

using Microsoft.Extensions.Logging;

using StackExchange.Redis;

/// <summary>
/// Redis реализация хранилища дедубликации
/// </summary>
public class RedisDeduplicationStore : IDeduplicationStore
{

    #region Constants

    private const string STORE_TYPE = "Redis";

    #endregion

    #region Fields

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisDeduplicationStore> _logger;
    private readonly DeduplicationOptions _options;
    private readonly IDeduplicationStore? _fallbackStore;
    private readonly bool _hasFallback;

    private IDatabase? _database;
    private bool _redisAvailable = true;
    private readonly IDeliveryMetrics? _metrics;

    #endregion

    #region Constructors

    public RedisDeduplicationStore(
        IConnectionMultiplexer redis,
        ILogger<RedisDeduplicationStore> logger,
        DeduplicationOptions options,
        IDeduplicationStore? fallbackStore = null,
        IDeliveryMetrics? metrics = null)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _fallbackStore = fallbackStore;
        _hasFallback = _options.EnableRedisFallback && _fallbackStore != null;

        _redis.ConnectionRestored += OnConnectionRestored;
        _redis.ConnectionFailed += OnConnectionFailed;

        _database = _redis.GetDatabase();
        _metrics = metrics;
    }

    #endregion

    #region Methods

    public async Task<bool> TryAddAsync(string messageId, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        string key = GetKey(messageId);
        var entry = new DeduplicationEntry
        {
            MessageId = messageId,
            ProcessedAt = DateTime.UtcNow,
            Ttl = ttl
        };

        string value = JsonSerializer.Serialize(entry);

        try
        {
            if (!_redisAvailable)
            {
                return await HandleRedisUnavailableAsync(() =>
                           _fallbackStore?.TryAddAsync(messageId, ttl, cancellationToken) ?? Task.FromResult(false));
            }

            IDatabase db = _database ?? _redis.GetDatabase();
            bool result = await db.StringSetAsync(key, value, ttl, When.NotExists);

            if (result)
            {
                _logger.LogTrace("Message {MessageId} added to Redis deduplication store, TTL: {Ttl}", messageId, ttl);
            }
            else
            {
                _logger.LogTrace("Message {MessageId} already exists in Redis deduplication store", messageId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis error during TryAdd for {MessageId}", messageId);
            _metrics?.DeduplicationStoreError(STORE_TYPE, ex.Message);
            _redisAvailable = false;

            if (_options.LogRedisErrors)
            {
                _logger.LogWarning(ex, "Redis unavailable, using fallback if configured");
            }

            return await HandleRedisUnavailableAsync(() =>
                       _fallbackStore?.TryAddAsync(messageId, ttl, cancellationToken) ?? Task.FromResult(false));
        }
    }

    public async Task RemoveAsync(string messageId, CancellationToken cancellationToken = default)
    {
        string key = GetKey(messageId);

        try
        {
            if (!_redisAvailable)
            {
                await HandleRedisUnavailableAsync(() =>
                    _fallbackStore?.RemoveAsync(messageId, cancellationToken) ?? Task.CompletedTask);

                return;
            }

            IDatabase db = _database ?? _redis.GetDatabase();
            await db.KeyDeleteAsync(key);
            _logger.LogTrace("Message {MessageId} removed from Redis deduplication store", messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis error during Remove for {MessageId}", messageId);
            _metrics?.DeduplicationStoreError(STORE_TYPE, ex.Message);
            _redisAvailable = false;

            await HandleRedisUnavailableAsync(() =>
                _fallbackStore?.RemoveAsync(messageId, cancellationToken) ?? Task.CompletedTask);
        }
    }

    public async Task<bool> ExistsAsync(string messageId, CancellationToken cancellationToken = default)
    {
        string key = GetKey(messageId);

        try
        {
            if (!_redisAvailable)
            {
                return await HandleRedisUnavailableAsync(() =>
                           _fallbackStore?.ExistsAsync(messageId, cancellationToken) ?? Task.FromResult(false));
            }

            IDatabase db = _database ?? _redis.GetDatabase();
            return await db.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis error during Exists for {MessageId}", messageId);
            _metrics?.DeduplicationStoreError(STORE_TYPE, ex.Message);
            _redisAvailable = false;

            return await HandleRedisUnavailableAsync(() =>
                       _fallbackStore?.ExistsAsync(messageId, cancellationToken) ?? Task.FromResult(false));
        }
    }

    public async Task<IReadOnlyCollection<string>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var keys = new List<string>();

        try
        {
            if (!_redisAvailable)
            {
                return await HandleRedisUnavailableAsync(() =>
                           _fallbackStore?.GetAllAsync(cancellationToken) ?? Task.FromResult<IReadOnlyCollection<string>>(Array.Empty<string>()));
            }

            IServer server = _redis.GetServer(_redis.GetEndPoints().First());
            string pattern = $"{_options.KeyPrefix}*";

            await foreach (RedisKey key in server.KeysAsync(pattern: pattern))
            {
                string keyStr = key.ToString();

                if (keyStr.StartsWith(_options.KeyPrefix))
                {
                    keys.Add(keyStr[_options.KeyPrefix.Length..]);
                }
            }

            return keys.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis error during GetAll");
            _redisAvailable = false;

            return await HandleRedisUnavailableAsync(() =>
                       _fallbackStore?.GetAllAsync(cancellationToken) ?? Task.FromResult<IReadOnlyCollection<string>>(Array.Empty<string>()));
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redisAvailable)
            {
                await HandleRedisUnavailableAsync(() =>
                    _fallbackStore?.ClearAsync(cancellationToken) ?? Task.CompletedTask);

                return;
            }

            EndPoint[] endpoints = _redis.GetEndPoints();

            foreach (EndPoint endpoint in endpoints)
            {
                IServer server = _redis.GetServer(endpoint);
                string pattern = $"{_options.KeyPrefix}*";

                await foreach (RedisKey key in server.KeysAsync(pattern: pattern))
                {
                    await _redis.GetDatabase().KeyDeleteAsync(key);
                }
            }

            _logger.LogInformation("Redis deduplication store cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis error during Clear");
            _redisAvailable = false;

            await HandleRedisUnavailableAsync(() =>
                _fallbackStore?.ClearAsync(cancellationToken) ?? Task.CompletedTask);
        }
    }

    public ValueTask DisposeAsync()
    {
        _redis.ConnectionRestored -= OnConnectionRestored;
        _redis.ConnectionFailed -= OnConnectionFailed;

        return ValueTask.CompletedTask;
    }

    private string GetKey(string messageId)
    {
        return $"{_options.KeyPrefix}{messageId}";
    }

    private void OnConnectionRestored(object? sender, ConnectionFailedEventArgs e)
    {
        _logger.LogInformation("Redis connection restored");
        _redisAvailable = true;
        _database = _redis.GetDatabase();
    }

    private void OnConnectionFailed(object? sender, ConnectionFailedEventArgs e)
    {
        _logger.LogWarning(e.Exception, "Redis connection failed");
        _redisAvailable = false;
    }

    private async Task<T> HandleRedisUnavailableAsync<T>(Func<Task<T>> fallbackAction)
    {
        if (_hasFallback && _fallbackStore != null)
        {
            _logger.LogDebug("Using In-Memory fallback for Redis operation");
            return await fallbackAction();
        }

        if (_options.LogRedisErrors)
        {
            _logger.LogWarning("Redis unavailable and no fallback configured, operation may fail");
        }

        return await fallbackAction();
    }

    private async Task HandleRedisUnavailableAsync(Func<Task> fallbackAction)
    {
        if (_hasFallback && _fallbackStore != null)
        {
            _logger.LogDebug("Using In-Memory fallback for Redis operation");
            await fallbackAction();
        }
        else if (_options.LogRedisErrors)
        {
            _logger.LogWarning("Redis unavailable and no fallback configured, operation may fail");
        }
    }

    #endregion

}
